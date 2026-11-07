import { HTTP_INTERCEPTORS, provideHttpClient, withFetch, withInterceptorsFromDi } from '@angular/common/http';
import {
  ApplicationConfig,
  EnvironmentProviders,
  Provider,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import {
  MSAL_GUARD_CONFIG,
  MSAL_INSTANCE,
  MSAL_INTERCEPTOR_CONFIG,
  MsalBroadcastService,
  MsalGuard,
  MsalGuardConfiguration,
  MsalInterceptor,
  MsalInterceptorConfiguration,
  MsalService
} from '@azure/msal-angular';
import { InteractionType, PublicClientApplication } from '@azure/msal-browser';

import { routes } from './app.routes';
import { authorityFor, entraConfig, entraConfigured } from './core/auth.config';

/** MSAL wiring, only added when Entra is configured. */
function msalProviders(): Provider[] {
  const instance = new PublicClientApplication({
    auth: {
      clientId: entraConfig.clientId,
      authority: authorityFor(entraConfig.tenantId),
      redirectUri: window.location.origin,
      postLogoutRedirectUri: window.location.origin
    },
    cache: {
      cacheLocation: 'localStorage'
    }
  });

  const guardConfig: MsalGuardConfiguration = {
    interactionType: InteractionType.Redirect,
    authRequest: { scopes: [entraConfig.apiScope] }
  };

  const interceptorConfig: MsalInterceptorConfiguration = {
    interactionType: InteractionType.Redirect,
    protectedResourceMap: new Map<string, string[]>([
      ['/api', [entraConfig.apiScope]]
    ])
  };

  return [
    { provide: MSAL_INSTANCE, useValue: instance },
    { provide: MSAL_GUARD_CONFIG, useValue: guardConfig },
    { provide: MSAL_INTERCEPTOR_CONFIG, useValue: interceptorConfig },
    MsalService,
    MsalGuard,
    MsalBroadcastService,
    { provide: HTTP_INTERCEPTORS, useClass: MsalInterceptor, multi: true }
  ];
}

const providers: (Provider | EnvironmentProviders)[] = [
  provideBrowserGlobalErrorListeners(),
  provideZoneChangeDetection({ eventCoalescing: true }),
  provideRouter(routes, withComponentInputBinding()),
  provideHttpClient(withFetch(), withInterceptorsFromDi())
];

if (entraConfigured()) {
  providers.push(...msalProviders());
}

export const appConfig: ApplicationConfig = { providers };
