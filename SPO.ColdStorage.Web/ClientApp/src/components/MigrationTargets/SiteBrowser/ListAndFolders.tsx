import React from 'react';
import { ListFolderConfig } from '../TargetSitesInterfaces';
import { FolderList } from './FolderList';
import { SPAuthInfo } from './SPDefs';
import { TreeItem } from '@mui/lab';
import { Checkbox, FormControlLabel } from "@mui/material";

interface Props {
    spoAuthInfo: SPAuthInfo,
    targetList: ListFolderConfig,
    folderRemoved: Function,
    folderAdd: Function,
    listRemoved: Function,
    listAdd: Function
}

export const ListFolders: React.FC<Props> = (props) => {
    const [checked, setChecked] = React.useState<boolean>(false);
    const [list, setList] = React.useState<ListFolderConfig | null>();

    const checkChange = (checked: boolean) => {
        setChecked(checked);
        if (checked)
            props.listAdd(props.targetList);
        else
            props.listRemoved(props.targetList);
    }

    const folderRemoved = (folder : string) => {
        props.folderRemoved(folder, props.targetList);
    }
    const folderAdd = (folder : string) => {
        props.folderAdd(folder, props.targetList);
    }

    React.useEffect(() => {
        setList(props.targetList);
    }, [props.targetList]);

    React.useEffect(() => {
        // Default checked value
        setChecked(props.targetList.includeInMigration);
    }, [props.targetList]);

    if (list)
    {
        return (
            <TreeItem
                key={list!.listTitle}
                nodeId={list!.listTitle}
                label={
                    <FormControlLabel
                        control={
                            <Checkbox checked={checked}
                                onChange={event => checkChange(event.currentTarget.checked)}
                                onClick={e => e.stopPropagation()}
                            />
                        }
                        label={<>{props.targetList.listTitle}</>}
                    />
                }
            >
                <FolderList folderWhiteList={props.targetList.folderWhiteList} 
                    folderAdd={(f : string)=> folderAdd(f)}  folderRemoved={(f : string)=> folderRemoved(f)} />
    
            </TreeItem>
        );
    }
    else
        return <div></div>;
}
