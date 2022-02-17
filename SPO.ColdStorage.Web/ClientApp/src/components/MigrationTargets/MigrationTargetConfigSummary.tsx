import React from 'react';
import { TargetMigrationSite, ListFolderConfig } from './TargetSitesInterfaces';
import Button from '@mui/material/Button';

interface Props {
    token: string,
    targetSite: TargetMigrationSite,
    removeSiteUrl: Function,
    selectLists: Function
}

export const MigrationTargetSite: React.FC<Props> = (props) => {

    const formatFolderName = (folderName : string) => 
    {
        if (folderName.endsWith("*")) {
            return "'" + folderName.substring(0, folderName.length - 1) + "' [plus sub-folders]"
        }
        else 
            return folderName;
    }

    return (
        <div>
            <span>{props.targetSite.rootURL}</span>
            <span><Button onClick={() => props.removeSiteUrl(props.targetSite)}>Remove</Button></span>
            <ul>
                {props.targetSite.siteFilterConfig?.listFilterConfig === null || props.targetSite.siteFilterConfig?.listFilterConfig === undefined || props.targetSite.siteFilterConfig.listFilterConfig!.length === 0 ?
                    <li>Include all lists</li>
                    :
                    (
                        <div className='siteLists'>
                            {props.targetSite.siteFilterConfig!.listFilterConfig!.map((listFolderConfig: ListFolderConfig) => (
                                
                                <li key={listFolderConfig.listTitle}>{listFolderConfig.listTitle}
                                {listFolderConfig.folderWhiteList.length === 0 ?
                                    (<ul><li>[All folders]</li></ul>)
                                :
                                    (
                                        <ul>
                                            {listFolderConfig.folderWhiteList.map((folder: string) => (
                                                <li key={folder}>{formatFolderName(folder)}</li>
                                            ))}
                                        </ul>
                                    )
                                }
                                </li>
                            ))}
                        </div>
                    )
                }
                <li><Button onClick={() => props.selectLists(props.targetSite)}>Select lists and folders</Button></li>
            </ul>
        </div>
    );
}
