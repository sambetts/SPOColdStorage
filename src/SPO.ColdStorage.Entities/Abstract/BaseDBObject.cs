using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPO.ColdStorage.Entities.Abstract
{
    /// <summary>
    /// Base database object
    /// </summary>
    public abstract class BaseDBObject
    {
        public bool IsUnsaved => this.ID > 0;

        [Key]
        [Column("id")]
        public int ID { get; set; }


        public override string ToString()
        {
            return $"{this.GetType().Name} ID={ID}";
        }
    }

    public abstract class BaseDBObjectWithUrl : BaseDBObject
    {
        [Required]
        [Column("url")]
        public string Url { get; set; } = string.Empty;

    }

    public abstract class BaseFileRelatedClass : BaseDBObject
    {
        [ForeignKey(nameof(File))]
        [Column("file_id")]
        public int FileId { get; set; }

        [Required]
        public DBEntities.SPFile File { get; set; } = new DBEntities.SPFile();
    }
}
