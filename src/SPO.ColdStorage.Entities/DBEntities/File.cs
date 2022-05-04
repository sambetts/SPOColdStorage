using SPO.ColdStorage.Entities.Abstract;
using SPO.ColdStorage.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPO.ColdStorage.Entities.DBEntities
{
    [Table("files")]
    public class SPFile : BaseDBObjectWithUrl
    {
        public SPFile() { }
        public SPFile(SharePointFileInfo fileDiscovered, Web parentWeb) : this()
        {
            this.Url = fileDiscovered.FullSharePointUrl;
            this.Web = parentWeb;
        }

        [ForeignKey(nameof(Web))]
        [Column("web_id")]
        public int WebId { get; set; }

        public Web Web { get; set; } = null!;

        [Column("last_modified")]
        public DateTime LastModified { get; set; } = DateTime.MinValue;

        [Column("last_modified_by")]
        public string LastModifiedBy { get; set; } = string.Empty;
    }

    [Table("file_stats")]
    public class FileStats: BaseDBObject
    {
        [ForeignKey(nameof(File))]
        [Column("file_id")]
        public int FileId { get; set; }

        public SPFile File { get; set; } = new SPFile();

        [Column("access_count")]
        public int? AccessCount { get; set; } = null;

        [Column("updated")]
        public DateTime Updated { get; set; }
    }
}
