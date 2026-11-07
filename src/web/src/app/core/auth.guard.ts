import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { MsalService } from '@azure/msal-angular';

import { entraConfig, entraConfigured } from './auth.config';

/**
 * Sends the user to Microsoft when Entra is configured and nobody is signed in.
 * A no-op otherwise, so the app stays usable without an app registration.
 */
export const authGuard: CanActivateFn = () => {
  if (!entraConfigured()) {
    return true;
  }

  const msal = inject(MsalService, { optional: true });
  if (!msal) {
    return true;
  }

  if (msal.instance.getAllAccounts().length > 0) {
    return true;
  }

  msal.loginRedirect({ scopes: [entraConfig.apiScope] });
  return false;
};
