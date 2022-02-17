import '../NavMenu.css';
import React from 'react';
import { NewTargetForm } from './NewTargetForm'
import { MigrationTargetSite } from './MigrationTargetConfigSummary'
import Button from '@mui/material/Button';

import { SelectedSiteBrowserDiag } from './SiteBrowser/SelectedSiteBrowserDiag';
import { ListFolderConfig, SiteListFilterConfig } from './TargetSitesInterfaces';

import update from 'immutability-helper';

export const MigrationTargetsConfig: React.FC<{ token: string }> = (props) => {

  const [loading, setLoading] = React.useState<boolean>(false);
  const [targetMigrationSites, setTargetMigrationSites] = React.useState<Array<SiteListFilterConfig>>([]);

  // Dialogue box for a site list-picker opens when this isn't null
  const [selectedSiteForDialogue, setSelectedSiteForDialogue] = React.useState<SiteListFilterConfig | null>(null);

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
        const data: SiteListFilterConfig[] = await response.json();
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
      .then((loadedTargetSites: SiteListFilterConfig[]) => {

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

    const newSiteDef: SiteListFilterConfig =
    {
      rootURL: newSiteUrl,
      listFilterConfig: []
    }
    setTargetMigrationSites(s => [...s, newSiteDef]);
  };

  const removeSiteUrl = (selectedSite: SiteListFilterConfig) => {
    const idx = targetMigrationSites.indexOf(selectedSite);
    if (idx > -1) {
      targetMigrationSites.splice(idx);
      setTargetMigrationSites(s => s.filter((value, i) => i !== idx));
    }
  };

  const selectLists = (selectedSite: SiteListFilterConfig) => {
    setSelectedSiteForDialogue(selectedSite);
  };


  // List & folder selection events
  const folderRemoved = (folder: string, list: ListFolderConfig, site: SiteListFilterConfig) => {

    // Find the roit site
    const siteIdx = targetMigrationSites.indexOf(site);
    if (siteIdx > -1) {

      const listIdx = site.siteFilterConfig!.listFilterConfig.indexOf(list);
      if (listIdx > -1) {

        // Update model to send. Different from child state for display
        const folderIdx = list.folderWhiteList.indexOf(folder);
        if (folderIdx > -1) {

          // Remove folder at specified location, in specified list, in specified site
          var targetMigrationSitesUpdate = update(targetMigrationSites,
            {
              [siteIdx]:
              {
                listFilterConfig:
                {
                  [listIdx]:
                  {
                    folderWhiteList:
                    {
                      $splice: [[folderIdx, 1]]
                    }
                  }
                }
              }
            }
          );

          // Update all sites state
          setTargetMigrationSites(targetMigrationSitesUpdate);

          // Reset the dialogue site for UI
          const updatedSite = targetMigrationSitesUpdate.find(s => s.rootURL === selectedSiteForDialogue?.rootURL);
          if (updatedSite) {
            setSelectedSiteForDialogue(updatedSite!);
          }
        }
      }
    }

  }
  const folderAdd = (folder: string, list: ListFolderConfig, site: SiteListFilterConfig) => {

    const siteIdx = targetMigrationSites.indexOf(site);
    if (siteIdx > -1) {

      const listIdx = site.listFilterConfig.indexOf(list);
      if (listIdx > -1) {

        // Update model to send. Different from child state for display
        const folderIdx = list.folderWhiteList.indexOf(folder);
        if (folderIdx === -1) {

          // Remove folder at specified location, in specified list, in specified site
          var targetMigrationSitesUpdate = update(targetMigrationSites,
            {
              [siteIdx]:
              {
                listFilterConfig:
                {
                  [listIdx]:
                  {
                    folderWhiteList:
                    {
                      $push: [folder]
                    }
                  }
                }
              }
            }
          );

          // Update all sites state
          setTargetMigrationSites(targetMigrationSitesUpdate);

          // Reset the dialogue site for UI
          const updatedSite = targetMigrationSitesUpdate.find(s => s.rootURL === selectedSiteForDialogue?.rootURL);
          if (updatedSite) {
            setSelectedSiteForDialogue(updatedSite!);
          }
        }
        else
          alert('Folder already added');
      }
    }

  }
  const listRemoved = (list: string, site: SiteListFilterConfig) => {

    const siteIdx = targetMigrationSites.indexOf(site);
    if (siteIdx > -1) {

      let targetListConfig: ListFolderConfig | null = null;
      site.siteFilterConfig!.listFilterConfig.forEach((listConfig: ListFolderConfig) => {
        if (listConfig.listTitle === list) {
          targetListConfig = listConfig;
        }
      });

      if (targetListConfig) {
        const listIdx = site.siteFilterConfig!.listFilterConfig.indexOf(targetListConfig);
        if (listIdx > -1) {

          var targetMigrationSitesUpdate = update(targetMigrationSites,
            {
              [siteIdx]:
              {
                listFilterConfig:
                {
                  $splice: [[listIdx, 1]]
                }
              }
            }
          );


          // Update all sites state
          setTargetMigrationSites(targetMigrationSitesUpdate);

          // Reset the dialogue site for UI
          const updatedSite = targetMigrationSitesUpdate.find(s => s.rootURL === selectedSiteForDialogue?.rootURL);
          if (updatedSite) {
            setSelectedSiteForDialogue(updatedSite!);
          }
        }

      }
    }
  }
  const listAdd = (list: string, site: SiteListFilterConfig) => {

    const siteIdx = targetMigrationSites.indexOf(site);
    if (siteIdx > -1) {

      const newList: ListFolderConfig = { listTitle: list, folderWhiteList: [] };
      var targetMigrationSitesUpdate = update(targetMigrationSites,
        {
          [siteIdx]:
          {
            listFilterConfig:
            {
              $push: [newList]
            }
          }
        }
      );

      // Update all sites state
      setTargetMigrationSites(targetMigrationSitesUpdate);

      // Reset the dialogue site for UI
      const updatedSite = targetMigrationSitesUpdate.find(s => s.rootURL === selectedSiteForDialogue?.rootURL);
      if (updatedSite) {
        setSelectedSiteForDialogue(updatedSite!);
      }
    }
  }

  const saveAll = () => {
    setLoading(true);
    fetch('AppConfiguration/SetMigrationTargets', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ' + props.token,
      },
      body: JSON.stringify(targetMigrationSites)
    }
    ).then(async response => {
      if (response.ok) {
        alert('Configuration saved to SQL');
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
                  {targetMigrationSites.map((SiteListFilterConfig: SiteListFilterConfig) => (
                    <MigrationTargetSite token={props.token} targetSite={SiteListFilterConfig}
                      removeSiteUrl={removeSiteUrl} selectLists={selectLists} key={SiteListFilterConfig.rootURL} />
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
        <SelectedSiteBrowserDiag token={props.token} targetSite={selectedSiteForDialogue}
          open={selectedSiteForDialogue !== null} onClose={closeDiag}
          folderAdd={(f: string, list: ListFolderConfig, site: SiteListFilterConfig) => folderAdd(f, list, site)}
          folderRemoved={(f: string, list: ListFolderConfig, site: SiteListFilterConfig) => folderRemoved(f, list, site)}
          listAdd={(list: string, site: SiteListFilterConfig) => listAdd(list, site)}
          listRemoved={(list: string, site: SiteListFilterConfig) => listRemoved(list, site)}
        />
      }
    </div>
  );
};
