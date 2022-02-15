import React from 'react';
import { ListFolderConfig, TargetMigrationSite } from '../TargetSitesInterfaces';
import { TreeView } from '@mui/lab';
import { ListFolders } from "./ListAndFolders";
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import { SPAuthInfo, SPList, SPListResponse } from './SPDefs';

interface Props {
    spoAuthInfo: SPAuthInfo,
    targetSite: TargetMigrationSite,
    folderRemoved: Function,
    folderAdd: Function,
    listRemoved: Function,
    listAdd: Function
}

export const SiteListsAndLibraries: React.FC<Props> = (props) => {
    const [listConfig, setListConfig] = React.useState<ListFolderConfig[] | null>(null);

    const getFilterConfigForSPList = React.useCallback((list: SPList): ListFolderConfig => {

        // Find config from existing list
        let listInfo : ListFolderConfig | null = null;
        props.targetSite.siteFilterConfig!.listFilterConfig.forEach((l: ListFolderConfig) => {
            if (l.listTitle === list.Title) {
                listInfo = l;
            }
        });

        // Or we are not currently tracking this list. Return default with "includeInMigration: false"
        if (!listInfo)
        {
            listInfo =
            {
                listTitle: list.Title,
                folderWhiteList: [] as string[],
                includeInMigration: false
            };
        }
        

        return listInfo;
    }, [props.targetSite]);

    React.useEffect(() => {

        // Load SharePoint lists from SPO REST
        fetch(`${props.targetSite.rootURL}/_api/web/lists`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                Accept: "application/json;odata=verbose",
                'Authorization': 'Bearer ' + props.spoAuthInfo.bearer,
            }
        }
        )
            .then(async response => {

                var responseText = await response.text();
                const data: SPListResponse = JSON.parse(responseText);

                if (data.d?.results) {

                    // Convert SP objects into our own ListFolderConfig, based on what lists are selected for migration
                    const lists: SPList[] = data.d.results;
                    let allListConfig: ListFolderConfig[] = [];
                    lists.forEach((list: SPList) => {
                        allListConfig.push(getFilterConfigForSPList(list));
                    });

                    setListConfig(allListConfig);
                }
                else {
                    alert('Unexpected response from SharePoint for lists: ' + responseText);
                    return Promise.reject();
                }
            });
    }, [props.spoAuthInfo, props.targetSite, getFilterConfigForSPList]);

    
    const folderRemoved = (folder : string, list : ListFolderConfig) => {
        props.folderRemoved(folder, list, props.targetSite);
    }
    const folderAdd = (folder : string, list : ListFolderConfig) => {
        props.folderAdd(folder, list, props.targetSite);
    }

    const listRemoved = (list : ListFolderConfig) => {
        props.listRemoved(list, props.targetSite);
    }
    const listAdd = (list : ListFolderConfig) => {
        props.listAdd(list, props.targetSite);
    }


    return (
        <div>
            {listConfig === null ?
                (
                    <div>Loading...</div>
                )
                :
                (
                    <TreeView defaultCollapseIcon={<ExpandMoreIcon />} defaultExpandIcon={<ChevronRightIcon />} >
                        {listConfig.map((listConfig: ListFolderConfig) =>
                        (
                            <ListFolders spoAuthInfo={props.spoAuthInfo} targetList={listConfig} 
                                folderAdd={(f : string, list : ListFolderConfig)=> folderAdd(f, list)}
                                folderRemoved={(f : string, list : ListFolderConfig)=> folderRemoved(f, list)}
                                listAdd={() => listAdd(listConfig)} listRemoved={() => listRemoved(listConfig)}
                            />
                        )
                        )}
                    </TreeView>
                )
            }
        </div>
    );
}
