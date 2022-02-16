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
    const [spLists, setSpLists] = React.useState<SPList[] | null>(null);

    const getFilterConfigForSPList = React.useCallback((list: SPList): ListFolderConfig | null => {

        // Find config from existing list
        let listInfo : ListFolderConfig | null = null;
        if (props.targetSite.siteFilterConfig?.listFilterConfig && props.targetSite.siteFilterConfig.listFilterConfig) {
            props.targetSite.siteFilterConfig.listFilterConfig!.forEach((l: ListFolderConfig) => {
                if (l.listTitle === list.Title) {
                    listInfo = l;
                }
            });
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

                    const lists: SPList[] = data.d.results;
                    
                    setSpLists(lists);
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

    const listRemoved = (listName : string) => {
        props.listRemoved(listName, props.targetSite);
    }
    const listAdd = (listName : string) => {
        props.listAdd(listName, props.targetSite);
    }


    return (
        <div>
            {spLists === null ?
                (
                    <div>Loading...</div>
                )
                :
                (
                    <TreeView defaultCollapseIcon={<ExpandMoreIcon />} defaultExpandIcon={<ChevronRightIcon />} >
                        {spLists.map((splist: SPList) =>
                        (
                            <ListFolders spoAuthInfo={props.spoAuthInfo} list={splist} targetListConfig={getFilterConfigForSPList(splist)} 
                                folderAdd={(f : string, list : ListFolderConfig)=> folderAdd(f, list)}
                                folderRemoved={(f : string, list : ListFolderConfig)=> folderRemoved(f, list)}
                                listAdd={() => listAdd(splist.Title)} listRemoved={() => listRemoved(splist.Title)}
                            />
                        )
                        )}
                    </TreeView>
                )
            }
        </div>
    );
}
