import React from "react";
import { useMsal } from "@azure/msal-react";
import { loginRequest } from "../authConfig";
import { Button } from "@mui/material";

function handleLogin(instance: any) {
    instance.loginPopup(loginRequest).catch((e : Error) => {
        console.error(e);
    });
}

/**
 * Renders a button which, when selected, will open a popup for login
 */
export const SignInButton = () => {
    const { instance } = useMsal();

    return (
        <Button onClick={() => handleLogin(instance)}>Sign in to Azure AD</Button>
    );
}