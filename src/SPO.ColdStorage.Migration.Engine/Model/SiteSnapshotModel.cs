using SPO.ColdStorage.Entities.DBEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Migration.Engine.Model
{
    public class Snapshot
    {

        public List<SiteSnapshotModel> SiteSnapshots { get; set; } = new List<SiteSnapshotModel>();
    }
    public class SiteSnapshotModel
    {
        public DateTime Started { get; set; } = DateTime.Now;
        public DateTime? Finished { get; set; }

        public TargetMigrationSite Site { get; set; } = new TargetMigrationSite();

        public List<SiteFile> Files { get; set; } = new List<SiteFile>();
    }
    public class SiteFile
    {
        public string FileName { get; set; } = string.Empty;

        public int? AccessCount { get; set; }

        public FileType FileType { get; set; }
    }

    public enum FileType
    {
        Unknown,
        ListItemAttachement,
        DocumentLibraryFile
    }
}
