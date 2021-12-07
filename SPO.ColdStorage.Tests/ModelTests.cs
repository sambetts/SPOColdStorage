﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using SPO.ColdStorage.Migration.Engine.Model;
using System;

namespace SPO.ColdStorage.Tests
{
    [TestClass]
    public class ModelTests
    {
        [TestMethod]
        public void FileSearchModelTests()
        {
            // Normal SP path
            var searchObj1 = new FileSearchModel(new SharePointFileInfo
            { 
                FileRelativePath = "/sites/MigrationHost/Shared%20Documents/Blank%20Office%20PPT.pptx",
                SiteUrl = "https://m365x352268.sharepoint.com/sites/MigrationHost"
            });

            Assert.IsTrue(searchObj1.FoldersDeep == 3);


            // Normalish SP path
            var searchObj2 = new FileSearchModel(new SharePointFileInfo
            {
                FileRelativePath = "/sites/Blank%20Office%20PPT.pptx",
                SiteUrl = "https://m365x352268.sharepoint.com/sites/MigrationHost"
            });

            Assert.IsTrue(searchObj2.FoldersDeep == 1);



            // Invalid SP path
            var searchObj3 = new FileSearchModel(new SharePointFileInfo
            {
                FileRelativePath = "Blank%20Office%20PPT.pptx",
                SiteUrl = "https://m365x352268.sharepoint.com/sites/MigrationHost"
            });

            Assert.IsTrue(searchObj3.FoldersDeep == 0);
        }

        [TestMethod]
        public void SharePointFileInfoTests()
        {
            var emptyMsg1 = new SharePointFileInfo { };
            Assert.IsFalse(emptyMsg1.IsValidInfo);

            var halfEmptyMsg = new SharePointFileInfo { FileRelativePath = "/whatever" };
            Assert.IsFalse(halfEmptyMsg.IsValidInfo);


            var legitMsg = new SharePointFileInfo
            { 
                FileRelativePath = "/whatever", 
                SiteUrl = "https://m365x352268.sharepoint.com", 
                WebUrl = "https://m365x352268.sharepoint.com/subweb1",
                LastModified = DateTime.Now
            };
            Assert.IsTrue(legitMsg.IsValidInfo);
        }
    }
}
