using Microsoft.VisualStudio.TestTools.UnitTesting;
using SPO.ColdStorage.Migration.Engine.SnapshotBuilder;
using SPO.ColdStorage.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Tests
{
    [TestClass]
    public class SnapshotBuilderTests : AbstractTest
    {
        /// <summary>
        /// Tests SiteSnapshotModel.AnalysisFinished
        /// </summary>
        [TestMethod]
        public void ModelAnalysisFinishedTests()
        {
            var m = new SiteSnapshotModel();
            var l = new DocLib();
            m.Lists.Add(l);

            var f1 = new DocumentSiteWithMetadata { State = SiteFileAnalysisState.AnalysisPending };
            var f2 = new DocumentSiteWithMetadata { State = SiteFileAnalysisState.AnalysisInProgress };
            l.Files.AddRange(new DocumentSiteWithMetadata []{ f1, f2 });

            m.InvalidateCaches();
            Assert.IsFalse(m.AnalysisFinished);

            f1.State = SiteFileAnalysisState.Complete;
            m.InvalidateCaches();
            Assert.IsFalse(m.AnalysisFinished);

            f2.State = SiteFileAnalysisState.Complete;
            m.InvalidateCaches();
            Assert.IsTrue(m.AnalysisFinished);

        }

        //[TestMethod]
        //public async Task ImportTests()
        //{
        //    var m = new SiteSnapshotModel();
        //    var l = new DocLib();
        //    m.Lists.Add(l);

        //    var builder = new SiteModelBuilder(_config, _tracer, );

        //    for (int i = 0; i < 100; i++)
        //    {
        //        l.Files.Add(
        //            new BaseSharePointFileInfo
        //            {
        //                Author = $"User {i}",
        //                LastModified = DateTime.Now,
        //                ServerRelativeFilePath = $"/site1/File{i}",
        //                SiteUrl = "https://unittesting.sharepoint.com/sites/MigrationHost",
        //                WebUrl = "https://unittesting.sharepoint.com/sites/MigrationHost/subsite"
        //            });
        //    }

        //    await m.bui
        //}
    }
}
