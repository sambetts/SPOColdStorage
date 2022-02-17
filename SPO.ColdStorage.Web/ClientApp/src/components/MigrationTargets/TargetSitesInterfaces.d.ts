
interface TargetMigrationSite {
    rootURL: string;
    siteFilterConfig?: SiteListFilterConfig;
  }
  
  interface SiteListFilterConfig{
    listFilterConfig: ListFolderConfig[]
  }
  
  interface ListFolderConfig{
    listTitle: string;
    folderWhiteList: string[];
  }

  export {
    TargetMigrationSite,
    SiteListFilterConfig,
    ListFolderConfig
  }