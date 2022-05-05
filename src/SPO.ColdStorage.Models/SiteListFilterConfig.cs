using System.Text.Json.Serialization;

namespace SPO.ColdStorage.Models
{
    /// <summary>
    /// Configuration for what to filter for a site. 
    /// </summary>
    public class SiteListFilterConfig
    {
        [JsonIgnore]
        public bool IsValid => Uri.IsWellFormedUriString(RootURL, UriKind.Absolute);
        public string RootURL { get; set; } = string.Empty;
        /// <summary>
        /// Lists to filter on. If empty will allow all lists
        /// </summary>
        public List<ListFolderConfig> ListFilterConfig { get; set; } = new List<ListFolderConfig>();

        #region Rules Calculation

        public ListFolderConfig GetListFolderConfig(string listTitle)
        {
            if (ListFilterConfig.Count == 0)
            {
                return new ListFolderConfig();
            }
            else
            {
                var listConfig = FindListFolderConfig(listTitle);
                if (listConfig != null) 
                    return listConfig;
                else
                    return new ListFolderConfig();      // Allow all
            }
        }

        ListFolderConfig? FindListFolderConfig(string listTitle)
        {
            return ListFilterConfig.Where(l => l.ListTitle == listTitle).FirstOrDefault();
        }

        public bool IncludeListInMigration(string listTitle)
        {
            if (ListFilterConfig.Count == 0)
            {
                return true;
            }
            else
            {
                var listFolderConfig = FindListFolderConfig(listTitle);
                return listFolderConfig != null;
            }
        }

        public bool IncludeFolderInMigration(string listTitle, string folderUrl)
        {
            // No config set - allow all
            if (ListFilterConfig.Count == 0)
            {
                return true;
            }
            else
            {
                var listFolderConfig = FindListFolderConfig(listTitle);
                if (listFolderConfig == null)
                {
                    return false;
                }
                else
                {
                    return listFolderConfig.IncludeFolderInMigration(folderUrl);
                }
            }
        }

        #endregion

        #region Json Parsing

        public string ToJson()
        {
            return System.Text.Json.JsonSerializer.Serialize(this);
        }
        public static SiteListFilterConfig FromJson(string filterConfigJson)
        {
            if (string.IsNullOrEmpty(filterConfigJson))
            {
                throw new ArgumentException($"'{nameof(filterConfigJson)}' cannot be null or empty.", nameof(filterConfigJson));
            }

            return System.Text.Json.JsonSerializer.Deserialize<SiteListFilterConfig>(filterConfigJson)!;
        }
        #endregion
    }

    /// <summary>
    /// Folder whitelist for a list
    /// </summary>
    public class ListFolderConfig
    {
        public string ListTitle { get; set; } = string.Empty;

        public List<string> FolderWhiteList { get; set; } = new List<string>();
        public bool IncludeFolderInMigration(string url)
        {
            if (FolderWhiteList.Count == 0)
            {
                return true;
            }
            else
            {
                return FolderWhiteList.Where(f=> f.ToLower() == url.ToLower()).Any();
            }
        }

        public bool IncludeFolder(BaseSharePointFileInfo fileInfo)
        {
            return IncludeFolderInMigration(fileInfo.Subfolder);
        }
    }
}
