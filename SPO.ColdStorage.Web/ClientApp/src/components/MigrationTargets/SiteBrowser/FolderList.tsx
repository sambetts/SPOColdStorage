import React from 'react';

import { TreeItem } from '@mui/lab';
import { FormControlLabel, TextField } from "@mui/material";
import { Button } from 'reactstrap';

interface Props {
    folderWhiteList: string[],
    folderRemoved: Function,
    folderAdd: Function
}

export const FolderList: React.FC<Props> = (props) => {

    const [newFilterVal, setNewFilterVal] = React.useState<string>("");
    const [folders, setFolders] = React.useState<string[]>([]);

    React.useEffect(() => {
        setFolders(props.folderWhiteList);
    }, [props.folderWhiteList]);

    
    const removeFolder = (folder: string) => {
        
        const idx = folders.indexOf(folder);
        if (idx > -1) {
            setFolders(oldList => oldList.filter((value, i) => i !== idx));
        }

        props.folderRemoved(folder)
    }


    const newFolderValChange = (val: string) => {
        setNewFilterVal(val);
    };
    const addNewFilter = () => {
        setFolders(f=> ([...f, newFilterVal]));
        props.folderAdd(newFilterVal);
        setNewFilterVal("");
    }

    const keydown = (key: number) => {
        if (key === 13)
            addNewFilter();
    };

    return (
        <div>
            {folders.map((folder: string) =>
                <TreeItem key={folder}
                    nodeId={folder}
                    label={
                        <FormControlLabel
                            control={
                                <Button onClick={() => removeFolder(folder)} />
                            }
                            label={<>{folder}</>}
                            key={folder}
                        />
                    }>
                </TreeItem>
            )
            }
            <TreeItem
                nodeId="new"
                label={
                    <FormControlLabel
                        control={
                            <TextField value={newFilterVal} onKeyDown={event => keydown(event.keyCode)}
                                onChange={event => newFolderValChange(event.currentTarget.value)}
                            />
                        }
                        label="Add"
                    />
                }>
            </TreeItem>
        </div>
    );
}