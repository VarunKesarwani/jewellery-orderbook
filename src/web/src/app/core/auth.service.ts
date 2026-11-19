import { Injectable, inject, signal } from '@angular/core';
import { MsalService } from '@azure/msal-angular';

import { entraConfig, entraConfigured } from './auth.config';

/**
 * Thin wrapper over MSAL so the shell and the guard do not each know the details.
 * When Entra is not configured the service reports enabled=false and every method
 * is a no-op, which keeps local development unauthenticated.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly enabled = entraConfigured();

  private readonly msal = inject(MsalService, { optional: true });

  readonly signedIn = signal(false);
  readonly name = signal<string | null>(null);

  constructor() {
    if (!this.msal) {
      return;
    }

    // Finish any redirect coming back from Microsoft before reading the account.
    this.msal.instance
      .handleRedirectPromise()
      .then(() => this.refresh())
      .catch(() => this.refresh());

    this.msal.instance.addEventCallback((event) => {
      if (event.eventType === 'msal:loginSuccess' || event.eventType === 'msal:logoutSuccess') {
        this.refresh();
      }
    });
  }

  signIn(): void {
    this.msal?.loginRedirect({ scopes: [entraConfig.apiScope] });
  }

  signOut(): void {
    this.msal?.logoutRedirect({ postLogoutRedirectUri: window.location.origin });
  }

  private refresh(): void {
    const accounts = this.msal?.instance.getAllAccounts() ?? [];
    const account = accounts[0];

    this.signedIn.set(!!account);
    this.name.set(account?.name ?? account?.username ?? null);
  }
}
