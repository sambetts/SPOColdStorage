﻿using Microsoft.SharePoint.Client;
using SPO.ColdStorage.Migration.Engine.Utils;
using SPO.ColdStorage.Models;

namespace SPO.ColdStorage.Migration.Engine
{
    /// <summary>
    /// Finds files in a SharePoint site collection
    /// </summary>
    public class SiteListsAndLibrariesCrawler
    {
        #region Constructors & Privates

        private readonly ClientContext _spClient;
        private readonly DebugTracer _tracer;
        public event Func<SharePointFileInfo, Task>? _foundFileToMigrateCallback;

        public SiteListsAndLibrariesCrawler(ClientContext clientContext, DebugTracer tracer) : this(clientContext, tracer, null)
        {
        }

        public SiteListsAndLibrariesCrawler(ClientContext clientContext, DebugTracer tracer, Func<SharePointFileInfo, Task>? foundFileToMigrateCallback)
        {
            this._spClient = clientContext;
            this._tracer = tracer;
            this._foundFileToMigrateCallback = foundFileToMigrateCallback;
        }

        #endregion

        public async Task CrawlContextRootWebAndSubwebs(SiteListFilterConfig siteFolderConfig)
        {
            var rootWeb = _spClient.Web;
            await EnsureContextWebIsLoaded();
            _spClient.Load(rootWeb.Webs);
            await _spClient.ExecuteQueryAsyncWithThrottleRetries(_tracer);


            await ProcessWeb(rootWeb, siteFolderConfig);

            foreach (var subSweb in rootWeb.Webs)
            {
                await ProcessWeb(subSweb, siteFolderConfig);
            }
        }

        private async Task ProcessWeb(Web web, SiteListFilterConfig siteFolderConfig)
        {
            Console.WriteLine($"Reading web '{web.ServerRelativeUrl}'...");
            _spClient.Load(web.Lists);
            await _spClient.ExecuteQueryAsyncWithThrottleRetries(_tracer);

            foreach (var list in web.Lists)
            {
                _spClient.Load(list, l => l.IsSystemList);
                await _spClient.ExecuteQueryAsyncWithThrottleRetries(_tracer);

                // Do not search through system or hidden lists
                if (!list.Hidden && !list.IsSystemList)
                {
                    if (siteFolderConfig.IncludeListInMigration(list.Title))
                    {
                        var listCrawlConfig = siteFolderConfig.GetListFolderConfig(list.Title);
                        _tracer.TrackTrace($"Crawling '{list.Title}'...");
                        await CrawlList(list, listCrawlConfig);
                    }
                    else
                    {
                        _tracer.TrackTrace($"Ignoring '{list.Title}' - not configured to migrate.");
                    }
                }
                else
                {
                    _tracer.TrackTrace($"Ignoring system/hidden list '{list.Title}'.");
                }
            }
        }
        public async Task<List<SharePointFileInfo>> CrawlList(List list)
        {
            return await CrawlList(list, new ListFolderConfig());
        }
        public async Task<List<SharePointFileInfo>> CrawlList(List list, ListFolderConfig listFolderConfig)
        {
            await EnsureContextWebIsLoaded();
            _spClient.Load(list, l => l.BaseType, l => l.ItemCount, l => l.RootFolder);
            await _spClient.ExecuteQueryAsyncWithThrottleRetries(_tracer);

            SiteList? listModel = null;
            var results = new List<SharePointFileInfo>();

            var camlQuery = new CamlQuery();
            camlQuery.ViewXml = "<View Scope=\"RecursiveAll\"><Query>" +
                "<OrderBy><FieldRef Name='ID' Ascending='TRUE'/></OrderBy></Query><RowLimit Paged=\"TRUE\">5000</RowLimit></View>";

            // Large-list support & paging
            ListItemCollection listItems = null!;
            ListItemCollectionPosition currentPosition = null!;
            do
            {
                camlQuery.ListItemCollectionPosition = currentPosition;

                listItems = list.GetItems(camlQuery);
                _spClient.Load(listItems, l => l.ListItemCollectionPosition);

                if (list.BaseType == BaseType.DocumentLibrary)
                {
                    // Load docs
                    _spClient.Load(listItems,
                                     items => items.Include(
                                        item => item.Id,
                                        item => item.FileSystemObjectType,
                                        item => item["Modified"],
                                        item => item["Editor"],
                                        item => item.File.Exists,
                                        item => item.File.ServerRelativeUrl,
                                        item => item.File.VroomItemID,
                                        item => item.File.VroomDriveID
                                    )
                                );
                    listModel = new DocLib() { Title = list.Title, ServerRelativeUrl = list.RootFolder.ServerRelativeUrl };
                }
                else
                {
                    // Generic list, or similar enough. Load attachments
                    _spClient.Load(listItems,
                                     items => items.Include(
                                        item => item.Id,
                                        item => item.AttachmentFiles,
                                        item => item["Modified"],
                                        item => item["Editor"],
                                        item => item.File.Exists,
                                        item => item.File.ServerRelativeUrl
                                    )
                                );
                    listModel = new SiteList() { Title = list.Title, ServerRelativeUrl = list.RootFolder.ServerRelativeUrl };
                }

                try
                {
                    await _spClient.ExecuteQueryAsyncWithThrottleRetries(_tracer);
                }
                catch (System.Net.WebException ex)
                {
                    Console.WriteLine($"Got error reading list: {ex.Message}.");
                }

                // Remember position, if more than 5000 items are in the list
                currentPosition = listItems.ListItemCollectionPosition;
                foreach (var item in listItems)
                {
                    SharePointFileInfo? foundFileInfo = null;
                    if (list.BaseType == BaseType.GenericList)
                    {
                        results.AddRange(await ProcessListItemAttachments(item, listModel, listFolderConfig));
                    }
                    else if (list.BaseType == BaseType.DocumentLibrary)
                    {
                        foundFileInfo = await ProcessDocLibItem(item, listModel, listFolderConfig);
                    }
                    if (foundFileInfo != null)
                        results.Add(foundFileInfo!);
                }
            }
            while (currentPosition != null);

            return results;
        }

        /// <summary>
        /// Process a single document library item.
        /// </summary>
        private async Task<SharePointFileInfo?> ProcessDocLibItem(ListItem docListItem, SiteList listModel, ListFolderConfig listFolderConfig)
        {
            if (docListItem.FileSystemObjectType == FileSystemObjectType.File && docListItem.File.Exists)
            {

                var foundFileInfo = GetSharePointFileInfo(docListItem, docListItem.File.ServerRelativeUrl, listModel);
                if (listFolderConfig.IncludeFolder(foundFileInfo))
                {
                    if (_foundFileToMigrateCallback != null)
                    {
                        await this._foundFileToMigrateCallback(foundFileInfo);
                    }
                }
                else
                {
#if DEBUG
                    Console.WriteLine($"DEBUG: Ignoring doc {foundFileInfo.ServerRelativeFilePath}");
#endif
                }
                return foundFileInfo;

            }

            return null;
        }

        /// <summary>
        /// Process custom list item with possibly multiple attachments
        /// </summary>
        private async Task<List<SharePointFileInfo>> ProcessListItemAttachments(ListItem item, SiteList listModel, ListFolderConfig listFolderConfig)
        {
            var attachmentsResults = new List<SharePointFileInfo>();

            foreach (var attachment in item.AttachmentFiles)
            {
                var foundFileInfo = GetSharePointFileInfo(item, attachment.ServerRelativeUrl, listModel);
                if (listFolderConfig.IncludeFolder(foundFileInfo))
                {
                    if (_foundFileToMigrateCallback != null)
                    {
                        await this._foundFileToMigrateCallback(foundFileInfo);
                    }
                    attachmentsResults.Add(foundFileInfo);
                }
                else
                {
#if DEBUG
                    Console.WriteLine($"DEBUG: Ignoring attachment {foundFileInfo.ServerRelativeFilePath}");
#endif
                }
            }

            return attachmentsResults;
        }


        async Task EnsureContextWebIsLoaded()
        {
            var loaded = false;
            try
            {
                // Test if this will blow up
                var url = _spClient.Web.Url;
                url = _spClient.Site.Url;
                loaded = true;
            }
            catch (PropertyOrFieldNotInitializedException)
            {
                loaded = false;
            }

            if (!loaded)
            {
                _spClient.Load(_spClient.Web);
                _spClient.Load(_spClient.Site, s => s.Url);
                await _spClient.ExecuteQueryAsyncWithThrottleRetries(_tracer);
            }
        }

        SharePointFileInfo GetSharePointFileInfo(ListItem item, string url, SiteList listModel)
        {
            var dir = "";
            if (item.FieldValues.ContainsKey("FileDirRef"))
            {
                dir = item.FieldValues["FileDirRef"].ToString();
                if (dir!.StartsWith(listModel.ServerRelativeUrl))
                {
                    // Truncate list URL from dir value of item
                    dir = dir.Substring(listModel.ServerRelativeUrl.Length).TrimStart("/".ToCharArray());
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(item), "Can't find dir column");
            }

            var dt = DateTime.MinValue;
            if (DateTime.TryParse(item.FieldValues["Modified"]?.ToString(), out dt))
            {
                var authorFieldObj = item.FieldValues["Editor"];
                if (authorFieldObj != null)
                {
                    var authorVal = (FieldUserValue)authorFieldObj;
                    var author = !string.IsNullOrEmpty(authorVal.Email) ? authorVal.Email : authorVal.LookupValue;
                    var isGraphDriveItem = listModel is DocLib;

                    // Doc or list-item?
                    if (!isGraphDriveItem)
                    {
                        // No Graph IDs - probably a list item
                        return new SharePointFileInfo
                        {
                            Author = author,
                            ServerRelativeFilePath = url,
                            LastModified = dt,
                            WebUrl = _spClient.Web.Url,
                            SiteUrl = _spClient.Site.Url,
                            Subfolder = dir.TrimEnd("/".ToCharArray())
                        };
                    }
                    else
                    {
                        return new DriveItemSharePointFileInfo
                        {
                            Author = author,
                            ServerRelativeFilePath = url,
                            LastModified = dt,
                            WebUrl = _spClient.Web.Url,
                            SiteUrl = _spClient.Site.Url,
                            Subfolder = dir.TrimEnd("/".ToCharArray()),
                            GraphItemId = item.File.VroomItemID,
                            DriveId = item.File.VroomDriveID,
                        };
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(item), "Can't find author column");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(item), "Can't find modified column");
            }
        }
    }
}