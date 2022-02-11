import React from 'react';
import { TargetMigrationSite } from '../TargetSitesInterfaces';

import { SPList, SPFolder, SPFolderResponse, SPAuthInfo } from './SPDefs';
import { TreeItem } from '@mui/lab';
import { Checkbox, FormControlLabel } from "@mui/material";

interface Props {
    spoAuthInfo: SPAuthInfo,
    list: SPList,
    targetSite: TargetMigrationSite
}

export const SiteNode: React.FC<Props> = (props) => {

    const [checked, setChecked] = React.useState<boolean>(false);
    const [error, setError] = React.useState<string | null>(null);
    const [folders, setFolders] = React.useState<string[]>([]);

    const checkChange = (checked: boolean) => {
        setChecked(checked);
    }

    if (error === null) {
        return (
            <TreeItem
                key={props.list.Id}
                nodeId={props.list.Id}
                label={
                    <FormControlLabel
                        control={
                            <Checkbox checked={checked}
                                onChange={event => checkChange(event.currentTarget.checked)}
                                onClick={e => e.stopPropagation()}
                            />
                        }
                        label={<>{props.list.Title}</>}
                        key={props.list.Id}
                    />
                }
            >
                <div>
                    {
                        folders.map((folder: string) =>
                            <TreeItem
                                nodeId={folder}
                                label={
                                    <FormControlLabel
                                        control={
                                            <Checkbox checked={checked}
                                                onChange={event => checkChange(event.currentTarget.checked)}
                                                onClick={e => e.stopPropagation()}
                                            />
                                        }
                                        label={<>{folder}</>}
                                        key={folder}
                                    />
                                }>
                            </TreeItem>
                        )

                    }
                </div>

            </TreeItem>
        );
    }
    else
        return (
            <TreeItem
                key={props.list.Id}
                nodeId={props.list.Id}
                label={error}
            />
        );
}
