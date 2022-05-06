

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

        public List<SiteList> Lists { get; set; } = new List<SiteList>();

        private List<DocLib>? _docLibsCache = null;
        public List<DocLib> AllDocLibs
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

        public bool AnalysisFinished 
        {
            get 
            {
                var r = new List<DocumentSiteWithMetadata>(DocsByState(SiteFileAnalysisState.AnalysisPending));
                r.AddRange(DocsByState(SiteFileAnalysisState.AnalysisInProgress));
                r.AddRange(DocsByState(SiteFileAnalysisState.TransientError));
                return !r.Any();
            }
        } 


        private List<DocumentSiteWithMetadata>? _docsWithError = null;
        public List<DocumentSiteWithMetadata> DocsWithErrorAny
        {
            get
            {
                if (_docsWithError == null)
                {
                    _docsWithError = new List<DocumentSiteWithMetadata>(DocsByState(SiteFileAnalysisState.FatalError));
                    _docsWithError.AddRange(DocsByState(SiteFileAnalysisState.TransientError));
                }
                return _docsWithError;
            }
        }

        private List<DocumentSiteWithMetadata>? _docsCompleted = null;
        public List<DocumentSiteWithMetadata> DocsCompleted
        {
            get
            {
                if (_docsCompleted == null)
                {
                    _docsCompleted = DocsByState(SiteFileAnalysisState.Complete);
                }
                return _docsCompleted;
            }
        }

        #endregion

        Dictionary<SiteFileAnalysisState, List<DocumentSiteWithMetadata>> _docsByStateCache = new();
        public List<DocumentSiteWithMetadata> DocsByState(SiteFileAnalysisState state)
        {
            if (!_docsByStateCache.ContainsKey(state))
            { 
                var results = AllFiles
                        .Where(f => f is DocumentSiteWithMetadata && ((DocumentSiteWithMetadata)f).State == state)
                        .Cast<DocumentSiteWithMetadata>()
                        .ToList();
                _docsByStateCache.Add(state, results);
            }
            return _docsByStateCache[state];
        }

        public DocumentSiteWithMetadata UpdateDocItemAndInvalidateCaches(DriveItemSharePointFileInfo updatedDocInfo, ItemAnalyticsRepsonse.AnalyticsItemActionStat accessStats, VersionStorageInfo versionStorageInfo)
        {
            var docLib = AllDocLibs.Where(l => l.DriveId == updatedDocInfo.DriveId).SingleOrDefault();
            if (docLib == null) throw new ArgumentOutOfRangeException(nameof(updatedDocInfo), $"No library in model for drive Id {updatedDocInfo.DriveId}");

            var file = docLib.Documents.Where(d => d.GraphItemId == updatedDocInfo.GraphItemId).SingleOrDefault();
            if (file != null)
            {
                // Set downloaded metadata
                file.AccessCount = accessStats.ActionCount;
                file.VersionHistorySize = versionStorageInfo.TotalSize;
                file.VersionCount = versionStorageInfo.VersionCount;

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
        public void InvalidateCaches()
        {
            _allFilesCache = null;
            _docLibsCache = null;
            _docsCompleted = null;
            _docsWithError = null;
            _docsByStateCache.Clear();
        }
    }
}
