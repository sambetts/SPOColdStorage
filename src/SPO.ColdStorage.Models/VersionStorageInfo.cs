using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Models
{
    public class VersionStorageInfo
    {
        public long TotalSize { get; set; } = 0;
        public int VersionCount { get; set; } = 0;
    }
}
