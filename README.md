# Jewellery Order Book

A single-tenant order book for a jewellery business: customers, orders, delivery, payments and
reports. Angular on the phone, ASP.NET Core Minimal API on the laptop, Entra ID for identity.

This repository is a **walking skeleton**. The plumbing is real and runs end to end; the domain is
deliberately thin until the pricing rules and field lists are settled.

## What works today

- Minimal API with health endpoints, RFC 7807 problem details carrying stable error codes,
  CORS for the dev server, and Entra ID bearer validation (with a development fallback).
- Customers: list, get, create. Orders: list, get, create. Both in memory.
- Angular app with a shell, three routes, a typed API client and INR/gram formatting pipes.
- Dev proxy so `ng serve` talks to the API without CORS trouble.

## What is not built yet

- The pricing engine (weights, wastage, making charges, stones). `OrderLine.LineTotalPaise` is a
  placeholder and is marked as such in the source.
- SQLite persistence, the backup worker, the archive reader.
- Payments, delivery, reports.
- A real sign-in screen. The API runs with a development identity until Entra is configured.

## Prerequisites

- .NET SDK 10.0 or later
- Node.js 20.19+ (Angular 20). Node 22 LTS is recommended; upgrading the CLI to the next major is
  a single `ng update` once you are on Node 22+.
- A terminal, and optionally Docker for the compose file.

## Run it

Two terminals.

```bash
# terminal 1 - API on http://localhost:5080
cd src/api
dotnet run

# terminal 2 - web on http://localhost:4200
cd src/web
npm install
npm start
```

Open http://localhost:4200. The dev server proxies `/api` and `/health` to port 5080.

Quick check that the API is alive:

```bash
curl http://localhost:5080/health/live
curl http://localhost:5080/api/me
```

## Identity

The API is an OAuth resource server. It validates a bearer token issued by Entra ID and maps app
roles to policies: `Owner`, `Accountant`, `Viewer`.

Set these in `src/api/appsettings.Development.json` (or user secrets) to turn real validation on:

```json
{
  "Entra": {
    "TenantId": "<tenant-guid>",
    "Audience": "api://<api-app-client-id>"
  }
}
```

With them blank and `ASPNETCORE_ENVIRONMENT=Development`, the API authenticates every request as a
local `Owner` through `DevelopmentAuthenticationHandler` so you can build screens before the app
registration exists. Outside Development the API refuses to start without a tenant — that guard is
intentional.

The Angular side will use `@azure/msal-angular` with authorization code + PKCE. There is no client
secret anywhere in this repository, and there never should be: the SPA is a public client.

## Layout

```
src/
  api/                     ASP.NET Core Minimal API
    Contracts/             request and response records
    Domain/                entities and enums
    Endpoints/             route groups, one file per resource
    Extensions/            auth, CORS, helpers
    Infrastructure/        repository interfaces and in-memory implementations
  web/                     Angular application
    src/app/core/          API client and DTOs
    src/app/features/      dashboard, orders, customers
    src/app/shared/        formatting pipes
```

## Conventions

- Money is integer **paise** everywhere: in the database, on the wire, in TypeScript. It is only
  divided by 100 at the moment it is displayed.
- Weight is integer **milligrams**; carats are integer thousandths.
- Percentages are integer **basis points** (800 = 8%).
- Every error carries a `code` in the problem details so the client can branch without matching on
  message text.
- Endpoints are plural nouns; verbs appear only for real state transitions (`/confirm`, `/cancel`).

## Licence

Private business application. Public for reference; no warranty.
