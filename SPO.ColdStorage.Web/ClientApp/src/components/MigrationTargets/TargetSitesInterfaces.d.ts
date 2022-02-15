
interface TargetMigrationSite {
    rootURL: string;
    siteFilterConfig?: SiteListFilterConfig;
  }
  
  interface SiteListFilterConfig{
    listFilterConfig: ListFolderConfig[]
  }
  
  interface ListFolderConfig{
    listTitle: string;
    includeInMigration: boolean;
    folderWhiteList: string[];
  }

  export {
    TargetMigrationSite,
    SiteListFilterConfig,
    ListFolderConfig
  }