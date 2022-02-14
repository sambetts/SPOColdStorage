import React from 'react';
import { TargetMigrationSite } from '../TargetSitesInterfaces';
import { TreeView } from '@mui/lab';
import { SiteNode } from "./SiteNode";
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import { SPAuthInfo, SPList, SPListResponse } from './SPDefs';

interface Props {
    spoAuthInfo: SPAuthInfo,
    targetSite: TargetMigrationSite
}

export const SiteList: React.FC<Props> = (props) => {
    const [lists, setLists] = React.useState<SPList[] | null>(null);

    React.useEffect(() => {
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
                    setLists(data.d.results);
                }
                else {
                    alert('Unexpected response from SharePoint for lists: ' + responseText);
                    return Promise.reject();
                }
            });
    }, []);

    const onListToggle = (e: any, nodeId: string[]) => {
        // Do something
    }

    return (
        <div>
            {lists === null ?
                (
                    <div>Loading...</div>
                )
                :
                (
                    <TreeView onNodeToggle={onListToggle} defaultCollapseIcon={<ExpandMoreIcon />} defaultExpandIcon={<ChevronRightIcon />} >
                        {lists.map((node: SPList) =>
                        (
                            <SiteNode list={node} spoAuthInfo={props.spoAuthInfo} targetSite={props.targetSite} />
                        )
                        )}
                    </TreeView>
                )
            }
        </div>
    );
}
