import React from 'react';
import { SiteListFilterConfig, ListFolderConfig } from './TargetSitesInterfaces';
import Button from '@mui/material/Button';

interface Props {
    token: string,
    targetSite: SiteListFilterConfig,
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
                {props.targetSite.listFilterConfig === null || props.targetSite.listFilterConfig === undefined || props.targetSite.listFilterConfig!.length === 0 ?
                    <li>[Include all lists in site]</li>
                    :
                    (
                        <div className='siteLists'>
                            {props.targetSite.listFilterConfig!.map((listFolderConfig: ListFolderConfig) => (
                                
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
