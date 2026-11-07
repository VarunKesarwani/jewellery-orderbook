/**
 * Entra ID configuration.
 *
 * Fill these in from the app registrations and the app starts requiring sign-in.
 * Leave them blank and the app runs unauthenticated, which is what local development
 * and the current API fallback expect.
 *
 * What each value is, and where it comes from in Azure:
 *
 *   clientId      SPA app registration -> Overview -> Application (client) ID
 *   tenantId      Entra ID -> Overview -> Tenant ID
 *   apiClientId   API app registration -> Overview -> Application (client) ID
 *   apiScope      "api://<apiClientId>/access_as_user" — the scope the API exposes
 *
 * The redirect URI registered on the SPA must match where the app is served from,
 * e.g. http://localhost:4200 for development and https://<tailnet-name>.ts.net in use.
 */
export interface EntraConfig {
  clientId: string;
  tenantId: string;
  apiClientId: string;
  apiScope: string;
}

export const entraConfig: EntraConfig = {
  clientId: '',
  tenantId: '',
  apiClientId: '',
  apiScope: ''
};

/** True when enough is configured to actually talk to Entra. */
export const entraConfigured = (): boolean =>
  !!entraConfig.clientId && !!entraConfig.tenantId && !!entraConfig.apiScope;

export const authorityFor = (tenantId: string): string =>
  `https://login.microsoftonline.com/${tenantId}`;
