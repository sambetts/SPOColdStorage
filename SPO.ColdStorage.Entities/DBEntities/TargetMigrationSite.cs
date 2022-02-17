using SPO.ColdStorage.Entities.Abstract;
using SPO.ColdStorage.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPO.ColdStorage.Entities.DBEntities
{
    [Table("target_migration_sites")]
    public class TargetMigrationSite : BaseDBObject
    {
        public TargetMigrationSite()
        {
        }

        public TargetMigrationSite(SiteListFilterConfig target) : this()
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            this.RootURL = target.RootURL;
            this.FilterConfigJson = target.ToJson();
        }

        [Column("root_url")]
        public string RootURL { get; set; } = string.Empty;

        [Column("filter_config_json")]
        public string FilterConfigJson { get; set; } = string.Empty;

        public SiteListFilterConfig ToSiteListFilterConfig()
        {
            if (string.IsNullOrEmpty(this.FilterConfigJson))
            {
                // Default, include everything
                return new SiteListFilterConfig() { RootURL = this.RootURL };
            }
            else
            {
                var filterConfig = SiteListFilterConfig.FromJson(this.FilterConfigJson);
                filterConfig.RootURL = this.RootURL;
                return filterConfig;
            }
        }
    }
}
