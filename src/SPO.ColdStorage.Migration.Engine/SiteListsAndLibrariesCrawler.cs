using Microsoft.SharePoint.Client;
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

        private readonly Action? crawlComplete;
        public event Func<SharePointFileInfoWithList, Task>? _foundFileToMigrateCallback;

        public SiteListsAndLibrariesCrawler(ClientContext clientContext, DebugTracer tracer) : this(clientContext, tracer, null, null)
        {
        }

        public SiteListsAndLibrariesCrawler(ClientContext clientContext, DebugTracer tracer, Func<SharePointFileInfoWithList, Task>? foundFileToMigrateCallback, Action? crawlComplete)
        {
            this._spClient = clientContext;
            this._tracer = tracer;
            this.crawlComplete = crawlComplete;
            this._foundFileToMigrateCallback = foundFileToMigrateCallback;
        }

        #endregion

        public async Task StartCrawl(SiteListFilterConfig siteFolderConfig)
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
            crawlComplete?.Invoke();
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
                        _tracer.TrackTrace($"Ignoring '{list.Title}' - not configured to analyse.");
                    }
                }
                else
                {
                    _tracer.TrackTrace($"Ignoring system/hidden list '{list.Title}'.");
                }
            }
        }
        public async Task<List<BaseSharePointFileInfo>> CrawlList(List list)
        {
            return await CrawlList(list, new ListFolderConfig());
        }
        public async Task<List<BaseSharePointFileInfo>> CrawlList(List list, ListFolderConfig listFolderConfig)
        {
            await EnsureContextWebIsLoaded();
            _spClient.Load(list, l => l.BaseType, l => l.ItemCount, l => l.RootFolder);
            await _spClient.ExecuteQueryAsyncWithThrottleRetries(_tracer);

            SiteList? listModel = null;
            var results = new List<BaseSharePointFileInfo>();

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
                                        item => item["File_x0020_Size"],
                                        item => item.File.Exists,
                                        item => item.File.ServerRelativeUrl,
                                        item => item.File.VroomItemID,
                                        item => item.File.VroomDriveID
                                    )
                                );

                    // Set drive ID when 1st results come back
                    listModel = new DocLib()
                    {
                        Title = list.Title,
                        ServerRelativeUrl = list.RootFolder.ServerRelativeUrl
                    };
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
                    BaseSharePointFileInfo? foundFileInfo = null;
                    if (list.BaseType == BaseType.GenericList)
                    {
                        results.AddRange(await ProcessListItemAttachments(item, listModel, listFolderConfig));
                    }
                    else if (list.BaseType == BaseType.DocumentLibrary)
                    {
                        // We might be able get the drive Id from the actual list, but not sure how...get it from 1st item instead
                        var docLib = (DocLib)listModel;
                        if (string.IsNullOrEmpty(docLib.DriveId))
                        {
                            ((DocLib)listModel).DriveId = item.File.VroomDriveID;
                        }

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
        private async Task<BaseSharePointFileInfo?> ProcessDocLibItem(ListItem docListItem, SiteList listModel, ListFolderConfig listFolderConfig)
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
        private async Task<List<BaseSharePointFileInfo>> ProcessListItemAttachments(ListItem item, SiteList listModel, ListFolderConfig listFolderConfig)
        {
            var attachmentsResults = new List<BaseSharePointFileInfo>();

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

        SharePointFileInfoWithList GetSharePointFileInfo(ListItem item, string url, SiteList listModel)
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
                    long size = 0;

                    // Doc or list-item?
                    if (!isGraphDriveItem)
                    {
                        var sizeVal = item.FieldValues["SMTotalFileStreamSize"];
                        
                        if (sizeVal != null)
                            long.TryParse(sizeVal.ToString(), out size);

                        // No Graph IDs - probably a list item
                        return new SharePointFileInfoWithList
                        {
                            Author = author,
                            ServerRelativeFilePath = url,
                            LastModified = dt,
                            WebUrl = _spClient.Web.Url,
                            SiteUrl = _spClient.Site.Url,
                            Subfolder = dir.TrimEnd("/".ToCharArray()),
                            List = listModel,
                            FileSize = size
                        };
                    }
                    else
                    {
                        var sizeVal = item.FieldValues["File_x0020_Size"];

                        if (sizeVal != null)
                            long.TryParse(sizeVal.ToString(), out size);
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
                            List = listModel,
                            FileSize = size
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
