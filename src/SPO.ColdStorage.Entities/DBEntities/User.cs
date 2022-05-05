using SPO.ColdStorage.Entities.Abstract;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Entities.DBEntities
{
    [Table("users")]
    public class User :BaseDBObject
    {
        [Column("email")]
        public string Email { get; set; } = string.Empty;
    }
}
