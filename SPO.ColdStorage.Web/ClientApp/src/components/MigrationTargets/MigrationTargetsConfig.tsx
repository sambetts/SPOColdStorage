import '../NavMenu.css';
import React from 'react';
import { NewTargetForm } from './NewTargetForm'
import { MigrationTargetSite } from './MigrationTargetSite'
import Button from '@mui/material/Button';

import { SiteBrowserDiag } from './SiteBrowser/SiteBrowserDiag';
import { ListFolderConfig, TargetMigrationSite } from './TargetSitesInterfaces';

export const MigrationTargetsConfig: React.FC<{ token: string }> = (props) => {

  const [loading, setLoading] = React.useState<boolean>(false);
  const [targetMigrationSites, setTargetMigrationSites] = React.useState<Array<TargetMigrationSite>>([]);

  // Dialogue box for a site list-picker opens when this isn't null
  const [selectedSiteForDialogue, setSelectedSiteForDialogue] = React.useState<TargetMigrationSite | null>(null);

  const getMigrationTargets = React.useCallback(async (token) => {

    // Return list of configured sites & folders to migrate from own API
    return await fetch('AppConfiguration/GetMigrationTargets', {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ' + token,
      }
    }
    )
      .then(async response => {
        const data: TargetMigrationSite[] = await response.json();
        return Promise.resolve(data);
      })
      .catch(err => {

        console.error('Loading migration configuration failed:');
        console.error(err);

        setLoading(false);

        return Promise.reject();
      });
  }, []);

  React.useEffect(() => {

    // Load sites config from API
    getMigrationTargets(props.token)
      .then((loadedTargetSites: TargetMigrationSite[]) => {

        setTargetMigrationSites(loadedTargetSites);

      });

  }, [props.token, getMigrationTargets]);

  // Add new site URL
  const addNewSiteUrl = (newSiteUrl: string) => {
    targetMigrationSites.forEach(s => {
      if (s.rootURL === newSiteUrl) {
        alert('Already have that site');
        return;
      }
    });

    const newSiteDef: TargetMigrationSite =
    {
      rootURL: newSiteUrl
    }
    setTargetMigrationSites(s => [...s, newSiteDef]);
  };

  const removeSiteUrl = (selectedSite: TargetMigrationSite) => {
    const idx = targetMigrationSites.indexOf(selectedSite);
    if (idx > -1) {
      targetMigrationSites.splice(idx);
      setTargetMigrationSites(s => s.filter((value, i) => i !== idx));
    }
  };

  const selectLists = (selectedSite: TargetMigrationSite) => {
    setSelectedSiteForDialogue(selectedSite);
  };


  const folderRemoved = (folder: string, list: ListFolderConfig, site: TargetMigrationSite) => {

    // Find the roit site
    const siteIdx = targetMigrationSites.indexOf(site);
    if (siteIdx > -1) {
      var allButThisSite = targetMigrationSites.filter((value, i) => i !== siteIdx);

      // Update model to send. Different from child state for display
      const folderIdx = list.folderWhiteList.indexOf(folder);
      if (folderIdx > -1) {
        list.folderWhiteList = list.folderWhiteList.filter((value, i) => i !== folderIdx);
        alert("Folder removed");
      }
      //[...f, newFilterVal]
    }


  }
  const folderAdd = (folder: string, list: ListFolderConfig, site: TargetMigrationSite) => {
    // Update model to send. Different from child state for display
    list.folderWhiteList.push(folder);
    alert("Folder added");
  }

  const listRemoved = (list: string, site: TargetMigrationSite) => {
    alert("List removed");
  }
  const listAdd = (list: string, site: TargetMigrationSite) => {
    alert("List added");
  }

  const saveAll = () => {
    setLoading(true);
    fetch('migration', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ' + props.token,
      },
      body: JSON.stringify(
        {
          TargetSites: targetMigrationSites
        })
    }
    ).then(async response => {
      if (response.ok) {
        alert('Success');
      }
      else {
        alert(await response.text());
      }
      setLoading(false);

    })
      .catch(err => {

        // alert('Loading storage data failed');
        setLoading(false);
      });
  };

  const closeDiag = () => {
    setSelectedSiteForDialogue(null);
  }


  return (
    <div>
      <h1>Cold Storage Migration Targets</h1>

      <p>
        When the migration tools run, these sites will be indexed &amp; copied to cold-storage.
        You can filter by list/library and then folders too.
      </p>

      {!loading ?
        (
          <div>
            {targetMigrationSites.length === 0 ?
              <div>No sites to migrate</div>
              :
              (
                <div id='migrationTargets'>
                  {targetMigrationSites.map((targetMigrationSite: TargetMigrationSite) => (
                    <MigrationTargetSite token={props.token} targetSite={targetMigrationSite}
                      removeSiteUrl={removeSiteUrl} selectLists={selectLists} />
                  ))}

                </div>
              )
            }
            <NewTargetForm addUrlCallback={(newSite: string) => addNewSiteUrl(newSite)} />

            {targetMigrationSites.length > 0 &&
              <Button variant="contained" onClick={() => saveAll()}>Save Changes</Button>
            }
          </div>
        )
        : <div>Loading...</div>
      }

      {selectedSiteForDialogue &&
        <SiteBrowserDiag token={props.token} targetSite={selectedSiteForDialogue}
          open={selectedSiteForDialogue !== null} onClose={closeDiag}
          folderAdd={(f: string, list: ListFolderConfig, site: TargetMigrationSite) => folderAdd(f, list, site)}
          folderRemoved={(f: string, list: ListFolderConfig, site: TargetMigrationSite) => folderRemoved(f, list, site)}
          listAdd={(list: string, site: TargetMigrationSite) => listAdd(list, site)}
          listRemoved={(list: string, site: TargetMigrationSite) => listRemoved(list, site)}
        />
      }
    </div>
  );
};
