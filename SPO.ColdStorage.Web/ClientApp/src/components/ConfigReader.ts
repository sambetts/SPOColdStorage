
export const getStorageConfigFromAPI = async (token : string) => {
  return await fetch('AppConfiguration/GetServiceConfiguration', {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer ' + token,
    }
  }
  )
    .then(async response => {
      const data: ServiceConfiguration = await response.json();
      return Promise.resolve(data);
    });
};


export interface StorageInfo {
  sharedAccessToken: string,
  accountURI: string,
  containerName: string
}

export interface SearchConfiguration {
  indexName: string,
  serviceName: string,
  queryKey: string
}

export interface ServiceConfiguration {
  storageInfo: StorageInfo,
  searchConfiguration: SearchConfiguration
}
