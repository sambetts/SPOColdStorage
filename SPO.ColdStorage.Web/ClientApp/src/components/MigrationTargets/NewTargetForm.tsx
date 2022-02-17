import React from "react";

import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';

interface Props {
    addUrlCallback: Function
}
interface State {
    rootUrl: string,
}

export class NewTargetForm extends React.Component<Props, State> {
    constructor(props: any) {
        super(props);
        this.state = { rootUrl: '' };

        this.handleChange = this.handleChange.bind(this);
        this.handleSubmit = this.handleSubmit.bind(this);
    }

    handleChange(eventVal: string) {
        this.setState({ rootUrl: eventVal });
    }

    handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        if (this.props.addUrlCallback) {
            this.props.addUrlCallback(this.state.rootUrl);
            this.setState({ rootUrl: "" });
        }
    }

    render() {
        return (
            <form onSubmit={this.handleSubmit}>
                <TextField type="url" size="small" value={this.state.rootUrl} onChange={e => this.handleChange(e.target.value)} 
                    label="New site root URL" required />
                <Button type="submit" variant="outlined" size="large">Add</Button>
            </form>
        );
    }
}