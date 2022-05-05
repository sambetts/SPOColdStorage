

namespace SPO.ColdStorage.Models
{
    /// <summary>
    /// Snapshot of files in a site.
    /// </summary>
    public class SiteSnapshotModel
    {
        #region Props

        public DateTime Started { get; set; } = DateTime.Now;
        public DateTime? Finished { get; set; }

        List<SiteList> Lists { get; set; } = new List<SiteList>();

        private List<DocLib>? _docLibsCache = null;
        public List<DocLib> DocLibs 
        { 
            get 
            {
                if (_docLibsCache == null)
                { 
                    _docLibsCache = Lists.Where(f => f.GetType() == typeof(DocLib)).Cast<DocLib>().ToList();
                }
                return _docLibsCache;
            } 
        }

        List<BaseSharePointFileInfo>? _allFilesCache = null;
        public List<BaseSharePointFileInfo> AllFiles 
        {
            get 
            {
                if (_allFilesCache == null)
                {
                    _allFilesCache = Lists.SelectMany(l => l.Files).ToList();

                }
                return _allFilesCache;
            }
        }

        private List<DocumentSiteFile>? _docsPendingAnalysis = null;
        public List<DocumentSiteFile> DocsPendingAnalysis
        {
            get 
            {
                if (_docsPendingAnalysis == null)
                {
                    _docsPendingAnalysis = AllFiles
                        .Where(f => f is DocumentSiteFile && ((DocumentSiteFile)f).State == SiteFileAnalysisState.AnalysisPending)
                        .Cast<DocumentSiteFile>()
                        .ToList();
                }
                return _docsPendingAnalysis;
            }
        }

        private List<DocumentSiteFile>? _docsWithError = null;
        public List<DocumentSiteFile> DocsWithError
        {
            get
            {
                if (_docsWithError == null)
                {
                    _docsWithError = AllFiles
                        .Where(f => f is DocumentSiteFile && ((DocumentSiteFile)f).State == SiteFileAnalysisState.Error)
                        .Cast<DocumentSiteFile>()
                        .ToList();
                }
                return _docsWithError;
            }
        }


        private List<DocumentSiteFile>? _docsCompleted = null;
        public List<DocumentSiteFile> DocsCompleted
        {
            get
            {
                if (_docsCompleted == null)
                {
                    _docsCompleted = AllFiles
                        .Where(f => f is DocumentSiteFile && ((DocumentSiteFile)f).State == SiteFileAnalysisState.Complete)
                        .Cast<DocumentSiteFile>()
                        .ToList();
                }
                return _docsCompleted;
            }
        }

        #endregion

        public DocumentSiteFile UpdateDocItem(DriveItemSharePointFileInfo updatedDocInfo, ItemAnalyticsRepsonse.AnalyticsItemActionStat accessStats)
        {
            var docLib = DocLibs.Where(l => l.DriveId == updatedDocInfo.DriveId).SingleOrDefault();
            if (docLib == null) throw new ArgumentOutOfRangeException(nameof(updatedDocInfo), $"No library in model for drive Id {updatedDocInfo.DriveId}");

            var file = docLib.Documents.Where(d=> d.GraphItemId == updatedDocInfo.GraphItemId).SingleOrDefault();
            if (file != null)
            {
                file.AccessCount = accessStats.ActionCount;
                file.State = SiteFileAnalysisState.Complete;

                InvalidateCaches();

                return file;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(updatedDocInfo), $"No doc in model doc-lib with item Id {updatedDocInfo.GraphItemId}");
            }
        }

        public void AddFile(BaseSharePointFileInfo newFile, SiteList list)
        {
            lock (this)
            {
                var targetList = Lists.Where(l => l.Equals(list)).SingleOrDefault();
                if (targetList == null)
                {
                    targetList = list;
                    Lists.Add(targetList);
                }

                targetList.Files.Add(newFile);

                InvalidateCaches();
            }
        }
        void InvalidateCaches()
        {
            _docsPendingAnalysis = null;
            _allFilesCache = null;
            _docLibsCache = null;
        }
    }
}
