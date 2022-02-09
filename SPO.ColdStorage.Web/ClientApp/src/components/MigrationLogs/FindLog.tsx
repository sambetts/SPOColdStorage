import React from 'react';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import { getStorageConfigFromAPI, ServiceConfiguration } from '../ConfigReader'

interface SearchResult {
    content: string;
    metadata_storage_name: string;
    path: string;
}
interface SearchResponse {
    value: Array<SearchResult>;
}
interface SearchLogsState {
    searchLogs: Array<SearchResult>,
    loading: boolean,
    searchTerm: string;
    serviceConfiguration : ServiceConfiguration | null;
}
interface SearchLogsProps {
    token: string;
}

export class FindLog extends React.Component<SearchLogsProps, SearchLogsState> {
    static displayName = FindLog.name;

    constructor(props: SearchLogsProps) {
        super(props);
        this.state = { searchLogs: [], loading: false, searchTerm: "", serviceConfiguration: null };
    }

    componentDidMount() {

        getStorageConfigFromAPI(this.props.token).then((config: ServiceConfiguration) => 
        {
            this.setState({serviceConfiguration: config});
        });

        if (this.state.searchTerm !== "") {
            this.populateSearchLogsFromSearch();
        }
    }

    async populateSearchLogsFromSearch() {
        if (this.state.searchTerm.length > 0) {
            this.setState({ loading: true });

            // https://docs.microsoft.com/en-us/azure/search/query-lucene-syntax
            const searchExpression = 'search=*' + this.state.searchTerm;

            // Send search request to search service
            await fetch('https://' + this.state.serviceConfiguration?.searchConfiguration.serviceName + 
                    '.search.windows.net/indexes/' + this.state.serviceConfiguration?.searchConfiguration.indexName + 
                    '/docs?api-version=2021-04-30-Preview&' + searchExpression, {
                method: 'GET',
                headers: {
                  'Content-Type': 'application/json',
                  'api-key': this.state.serviceConfiguration!.searchConfiguration!.queryKey!,
                }})
                .then(async response => {
                    const data : SearchResponse = await response.json();
                    console.log(data);
                    this.setState({ searchLogs: data.value, loading: false });
                })
                .catch(err => {
                    alert('Loading data failed');
                    this.setState({ searchLogs: [], loading: false });
                });
        }
    }

    renderResultsTable(logs: Array<SearchResult>) {
        return (
            <div>
                {logs.length > 0 ?
                    <div>
                        <p>{logs.length} documents found.</p>
                        <table className='table table-striped' aria-labelledby="tabelLabel">
                            <thead>
                                <tr>
                                    <th>File name</th>
                                    <th></th>
                                </tr>
                            </thead>
                            <tbody>
                                {logs.map((log: SearchResult) =>
                                    <tr key={log.path}>
                                        <td>{log.path}</td>
                                        <td><a href={log.path + this.state.serviceConfiguration?.storageInfo.sharedAccessToken}>Download</a></td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                    :
                    <div>No files found in search index</div>
                }

            </div>
        );
    }

    render() {
        if (this.state.serviceConfiguration === null)
        {
            return (
                <div>Loading search configuration...</div>
            );
        }

        let contents = this.state.loading
            ? <p><em>Loading...</em></p>
            : this.renderResultsTable(this.state.searchLogs);

        return (
            <div>
                <h1 id="tabelLabel">Migration Logs</h1>
                <p>Search for a file that's been <strong>succesfully</strong> migrated to cold-storage.</p>
                <TextField id="outlined-basic" label="Search term" variant="standard" required
                    onChange={e => { this.setState({ searchTerm: e.target.value }); }} />
                <Button variant="outlined" size="large"
                    onClick={() => {
                        this.populateSearchLogsFromSearch();
                    }}
                >Search</Button>

                {contents}
            </div>
        );
    }

}
