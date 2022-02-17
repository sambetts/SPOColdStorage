
interface SiteListFilterConfig {
    rootURL: string;
    siteFilterConfig?: SiteListFilterConfig;
    listFilterConfig: ListFolderConfig[];
  }
  
  interface ListFolderConfig{
    listTitle: string;
    folderWhiteList: string[];
  }

  export {
    SiteListFilterConfig,
    ListFolderConfig
  }