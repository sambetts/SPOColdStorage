using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Models
{

    public class SiteList : IEquatable<SiteList>
    {
        public SiteList() { }
        public SiteList(SiteList l)
        {
            this.Title = l.Title;
            this.ServerRelativeUrl = l.ServerRelativeUrl;
        }

        public string Title { get; set; } = string.Empty;
        public string ServerRelativeUrl { get; set; } = string.Empty;
        public List<BaseSharePointFileInfo> Files { get; set; } = new List<BaseSharePointFileInfo>();

        public bool Equals(SiteList? other)
        {
            if (other == null) return false;
            return ServerRelativeUrl == other.ServerRelativeUrl && Title == other.Title;
        }
    }

    public class DocLib : SiteList
    {
        public DocLib() { }
        public DocLib(SiteList l) : base(l)
        {
            if (l is DocLib)
            {
                var lib = (DocLib)l;
                this.DriveId = lib.DriveId;
                this.Delta = lib.Delta;
                this.Files = lib.Files;
            }
        }
        public string DriveId { get; set; } = string.Empty;

        public List<DocumentSiteWithMetadata> Documents => Files.Where(f => f.GetType() == typeof(DocumentSiteWithMetadata)).Cast<DocumentSiteWithMetadata>().ToList();
        public string Delta { get; set; } = string.Empty;
    }

}
