﻿using System.Text.Json.Serialization;

namespace SPO.ColdStorage.Migration.Engine.Model
{
    public class SharePointFileLocationInfo
    {
        /// <summary>
        /// Example: https://m365x352268.sharepoint.com/sites/MigrationHost
        /// </summary>
        public string SiteUrl { get; set; } = string.Empty;

        /// <summary>
        /// Example: /sites/MigrationHost/Shared%20Documents/Contoso.pptx
        /// </summary>
        public string FileRelativePath { get; set; } = string.Empty;

        [JsonIgnore]
        public string FullUrl => SiteUrl + FileRelativePath;

        [JsonIgnore]
        public virtual bool IsValidInfo => !string.IsNullOrEmpty(FileRelativePath) && !string.IsNullOrEmpty(SiteUrl);
    }

    public class SharePointFileVersionInfo : SharePointFileLocationInfo
    {
        public DateTime LastModified { get; set; } = DateTime.MinValue;

        public override bool IsValidInfo => base.IsValidInfo && this.LastModified > DateTime.MinValue;
    }

    public class SharePointFileInfoEventArgs : EventArgs
    {
        public SharePointFileInfoEventArgs()
        {
            this.SharePointFileInfo = new SharePointFileVersionInfo();
        }
        public SharePointFileVersionInfo SharePointFileInfo { get; set; }
    }
}
