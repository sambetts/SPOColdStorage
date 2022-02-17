import React from 'react';
import { ListFolderConfig } from '../TargetSitesInterfaces';
import { FolderList } from './FolderList';
import { SPAuthInfo, SPList } from './SPDefs';
import { TreeItem } from '@mui/lab';
import { Checkbox, FormControlLabel } from "@mui/material";

interface Props {
    spoAuthInfo: SPAuthInfo,
    targetListConfig: ListFolderConfig | null,
    list: SPList,
    folderRemoved: Function,
    folderAdd: Function,
    listRemoved: Function,
    listAdd: Function
}

export const ListAndFolders: React.FC<Props> = (props) => {
    const [checked, setChecked] = React.useState<boolean>(false);

    const checkChange = (checked: boolean) => {
        setChecked(checked);
        if (checked)
            props.listAdd(props.list);
        else
            props.listRemoved(props.list);
    }

    const folderRemoved = (folder : string) => {        
        props.folderRemoved(folder, props.targetListConfig);
    }
    const folderAdd = (folder : string) => {
        props.folderAdd(folder, props.targetListConfig);
    }

    React.useEffect(() => {
        // Default checked value
        setChecked(props.targetListConfig !== null);
    }, [props.targetListConfig]);

    return (
        <TreeItem
            key={props.list.Title}
            nodeId={props.list.Title}
            label={
                <FormControlLabel
                    control={
                        <Checkbox checked={checked}
                            onChange={event => checkChange(event.currentTarget.checked)}
                            onClick={e => e.stopPropagation()}
                        />
                    }
                    label={<>{props.list.Title}</>}
                />
            }
        >
            {props.targetListConfig && props.targetListConfig.folderWhiteList &&
                <FolderList folderWhiteList={props.targetListConfig!.folderWhiteList} 
                    folderAdd={(f : string)=> folderAdd(f)}  folderRemoved={(f : string)=> folderRemoved(f)} />
            }
            

        </TreeItem>
    );
}
