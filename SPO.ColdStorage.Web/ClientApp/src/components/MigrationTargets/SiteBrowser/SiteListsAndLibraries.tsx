import React from 'react';
import { ListFolderConfig, SiteListFilterConfig } from '../TargetSitesInterfaces';
import { TreeView } from '@mui/lab';
import { ListAndFolders } from "./ListAndFolders";
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import { SPAuthInfo, SPList, SPListResponse } from './SPDefs';

interface Props {
    spoAuthInfo: SPAuthInfo,
    targetSite: SiteListFilterConfig,
    folderRemoved: Function,
    folderAdd: Function,
    listRemoved: Function,
    listAdd: Function
}

export const SiteListsAndLibraries: React.FC<Props> = (props) => {
    const [spLists, setSpLists] = React.useState<SPList[] | null>(null);
    const [targetSite, setTargetSite] = React.useState<SiteListFilterConfig>();

    React.useEffect(() => {
        setTargetSite(props.targetSite);
    }, [props.targetSite]);

    const getFilterConfigForSPList = React.useCallback((list: SPList): ListFolderConfig | null => {

        // Find config from existing list
        let listInfo : ListFolderConfig | null = null;
        if (targetSite!.listFilterConfig && targetSite!.listFilterConfig) {
            targetSite!.listFilterConfig!.forEach((l: ListFolderConfig) => {
                if (l.listTitle === list.Title) {
                    listInfo = l;
                }
            });
        }
        

        return listInfo;
    }, [targetSite]);

    

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

    const listRemoved = (list : string) => {
        props.listRemoved(list, props.targetSite);
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
                            <ListAndFolders spoAuthInfo={props.spoAuthInfo} list={splist} targetListConfig={getFilterConfigForSPList(splist)} 
                                folderAdd={(f : string, list : ListFolderConfig)=> folderAdd(f, list)}
                                folderRemoved={(f : string, list : ListFolderConfig)=> folderRemoved(f, list)}
                                listAdd={() => listAdd(splist.Title)} listRemoved={() => listRemoved(splist.Title)}
                                key={splist.Title}
                            />
                        )
                        )}
                    </TreeView>
                )
            }
        </div>
    );
}
