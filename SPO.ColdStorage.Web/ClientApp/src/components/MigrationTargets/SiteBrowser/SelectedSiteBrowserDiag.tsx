import React from 'react';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import CloseIcon from '@mui/icons-material/Close';


import { ListFolderConfig, SiteListFilterConfig } from '../TargetSitesInterfaces';
import { SiteListsAndLibraries } from './SiteListsAndLibraries';
import { SPAuthInfo } from './SPDefs';

interface Props {
    token: string,
    targetSite: SiteListFilterConfig,
    open: boolean,
    onClose: Function,
    folderRemoved: Function,
    folderAdd: Function,
    listRemoved: Function,
    listAdd: Function
}

export const SelectedSiteBrowserDiag: React.FC<Props> = (props) => {

    const handleClose = () => {
        props.onClose();
    };
    const [spoAuthInfo, setSpoAuthInfo] = React.useState<SPAuthInfo | null>(null);
    const [targetSite, setTargetSite] = React.useState<SiteListFilterConfig>();

    React.useEffect(() => {
        setTargetSite(props.targetSite);
    }, [props.targetSite]);

    // Load SPO auth from API
    React.useEffect(() => {

        if (!spoAuthInfo) {
            
            // Get SPO bearer token from API
            fetch('AppConfiguration/GetSharePointToken', {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + props.token,
                }
            })
                .then(response => {
                    response.text().then((spoAuthToken: string) => {

                        // Get SP digest with OAuth token
                        const url = `${props.targetSite.rootURL}/_api/contextinfo`;
                        fetch(url, {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json',
                                Accept: "application/json;odata=verbose",
                                'Authorization': 'Bearer ' + spoAuthToken,
                            }
                        })
                            .then(spoResponse => {

                                // Remember SPO auth code & digest
                                spoResponse.json().then((digestJson: any) => {
                                    setSpoAuthInfo({ bearer: spoAuthToken, digest: digestJson.d.GetContextWebInformation.FormDigestValue });
                                });

                            });

                    });
                })
                .catch(err => {
                    alert('Got error loading token');
                });
        }

    }, [props.targetSite.rootURL, props.token, spoAuthInfo]);


    const folderRemoved = (folder : string, list : ListFolderConfig) => {
        props.folderRemoved(folder, list, props.targetSite);
    }
    const folderAdd = (folder : string, list : ListFolderConfig) => {
        props.folderAdd(folder, list, props.targetSite);
    }

    const listRemoved = (list : string) => {
        props.listRemoved(list, props.targetSite);
    }
    const listAdd = (list : ListFolderConfig) => {
        props.listAdd(list, props.targetSite);
    }


    return (
        <div>
            {spoAuthInfo === null ?
                (
                    <div>Loading</div>
                )
                :
                (
                    <Dialog
                        fullScreen
                        open={props.open}
                        onClose={handleClose}>

                        <AppBar sx={{ position: 'relative' }}>
                            <Toolbar>
                                <IconButton
                                    edge="start"
                                    color="inherit"
                                    onClick={handleClose}
                                    aria-label="close">
                                    <CloseIcon />
                                </IconButton>
                                <Typography sx={{ ml: 2, flex: 1 }} variant="h6" component="div">
                                    Select Contents to Migrate: {targetSite!.rootURL}
                                </Typography>
                                <Button autoFocus color="inherit" onClick={handleClose}>
                                    Save
                                </Button>
                            </Toolbar>
                        </AppBar>
                        <SiteListsAndLibraries spoAuthInfo={spoAuthInfo!} targetSite={targetSite!} 
                            folderAdd={(f : string, list : ListFolderConfig)=> folderAdd(f, list)}
                            folderRemoved={(f : string, list : ListFolderConfig)=> folderRemoved(f, list)}
                            listAdd={(list : ListFolderConfig) => listAdd(list)} 
                            listRemoved={(list : string) => listRemoved(list)}
                        />

                    </Dialog>
                )
            }
        </div>
    );
}
