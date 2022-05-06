using SPO.ColdStorage.Models;

namespace SPO.ColdStorage.Migration.Engine.Utils.Extentions
{
    public static class DriveItemVersionsExtensions
    {
        public static VersionStorageInfo ToVersionStorageInfo(this IEnumerable<DriveItemVersion> driveItemVersions)
        {
            var results = new VersionStorageInfo();
            if (driveItemVersions != null)
            {
                foreach (var item in driveItemVersions)
                {
                    results.VersionCount++;
                    if (item.Size.HasValue)
                    {
                        results.TotalSize += item.Size.Value;
                    }
                }
            }
            
            return results;
        }
    }
}
