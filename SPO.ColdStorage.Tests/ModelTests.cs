using Microsoft.VisualStudio.TestTools.UnitTesting;
using SPO.ColdStorage.Models;
using System;
using System.Collections.Generic;

namespace SPO.ColdStorage.Tests
{
    [TestClass]
    public class ModelTests
    {
        [TestMethod]
        public void SharePointFileInfoTests()
        {
            var emptyMsg1 = new SharePointFileInfo { };
            Assert.IsFalse(emptyMsg1.IsValidInfo);

            var halfEmptyMsg = new SharePointFileInfo { ServerRelativeFilePath = "/subweb1/whatever.txt" };
            Assert.IsFalse(halfEmptyMsg.IsValidInfo);

            // File path doesn't contain web
            var invalidMsg1 = new SharePointFileInfo
            { 
                ServerRelativeFilePath = "/whatever", 
                SiteUrl = "https://m365x352268.sharepoint.com", 
                WebUrl = "https://m365x352268.sharepoint.com/subweb1",
                LastModified = DateTime.Now
            };
            Assert.IsFalse(invalidMsg1.IsValidInfo);

            // Trailing slashes
            var invalidMsg2 = new SharePointFileInfo
            {
                ServerRelativeFilePath = "/whatever",
                SiteUrl = "https://m365x352268.sharepoint.com/",
                WebUrl = "https://m365x352268.sharepoint.com/subweb1/",
                LastModified = DateTime.Now
            };
            Assert.IsFalse(invalidMsg2.IsValidInfo);

            // Missing start slash on file path
            var invalidMsg3 = new SharePointFileInfo
            {
                ServerRelativeFilePath = "subweb1/whatever",
                SiteUrl = "https://m365x352268.sharepoint.com",
                WebUrl = "https://m365x352268.sharepoint.com/subweb1",
                LastModified = DateTime.Now
            };
            Assert.IsFalse(invalidMsg3.IsValidInfo);

            // Valid test; no folders
            var validMsg1 = new SharePointFileInfo
            {
                ServerRelativeFilePath = "/subweb1/whatever",
                SiteUrl = "https://m365x352268.sharepoint.com",
                WebUrl = "https://m365x352268.sharepoint.com/subweb1",
                LastModified = DateTime.Now
            };
            Assert.IsTrue(validMsg1.IsValidInfo);
            Assert.IsTrue(validMsg1.FullSharePointUrl == "https://m365x352268.sharepoint.com/subweb1/whatever");


            // Invalid folder - has leading/trailing slashes
            var invalidMsg4 = new SharePointFileInfo
            {
                ServerRelativeFilePath = "/subweb1/whatever",
                Subfolder = "/sub1/sub2",
                SiteUrl = "https://m365x352268.sharepoint.com",
                WebUrl = "https://m365x352268.sharepoint.com/subweb1",
                LastModified = DateTime.Now
            };
            Assert.IsFalse(invalidMsg4.IsValidInfo);

        }


        [TestMethod]
        public void SiteFolderConfigTests()
        {
            var cfg = new SiteListFilterConfig()
            {
                ListFilterConfig = new List<ListFolderConfig>
                            {
                                new ListFolderConfig{ ListTitle = "Documents" },
                                new ListFolderConfig{ ListTitle = "Custom List", 
                                    FolderWhiteList = new List<string>{ "Subfolder", "Subfolder/Another subfolder" } }
                            }
            };
            Assert.IsTrue(cfg.IncludeListInMigration("Documents"));
            Assert.IsFalse(cfg.IncludeListInMigration("Docs"));

            Assert.IsTrue(cfg.IncludeFolderInMigration("Custom List", "Subfolder"));
            Assert.IsTrue(cfg.IncludeFolderInMigration("Custom List", "Subfolder/Another subfolder"));
            Assert.IsFalse(cfg.IncludeFolderInMigration("Custom List", "Some other folder"));

            // Root folder not included if whitelist has items (without root in list)
            Assert.IsFalse(cfg.IncludeFolderInMigration("Custom List", ""));

            // Root folder is included if whitelist has no items
            Assert.IsTrue(cfg.IncludeFolderInMigration("Documents", ""));


            // No config set
            Assert.IsTrue(new SiteListFilterConfig().IncludeListInMigration("Documents"));
            Assert.IsTrue(new SiteListFilterConfig().IncludeFolderInMigration("Documents2", "whatever"));

        }
    }
}
