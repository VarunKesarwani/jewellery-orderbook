# Jewellery Order Book

A single-tenant order book for a jewellery business: customers, orders with metal and stone
components, delivery, payments and daily reports. Angular on the phone, ASP.NET Core Minimal API on
the laptop, Entra ID for identity.

## What works today

- **Pricing engine** — net weight, wastage, metal value, making charges, stone value, four optional
  tax components, discount and round-off. Pure functions, covered by 29 checks.
- **Customers** — name, phone and address mandatory; PAN, email, GSTIN, credit days and notes optional.
- **Orders** — created with a number and a status immediately, multiple lines, stones per line,
  per-component tax, cancel before delivery with an automatic refund.
- **Delivery** — per line, partial allowed, order flips to Delivered when every piece is out.
- **Payments** — recorded against an order, optionally against a specific delivered line; the order's
  payment state is derived (Unpaid / PartiallyPaid / Paid).
- **Order entry screen** — search an existing customer or create one inline, metal block, stones, the four tax boxes, and live
  totals priced by the server on every keystroke (`POST /api/orders/calculate`), so the preview
  cannot drift from what gets stored.
- **Order detail** — items with delivered and remaining counts, delivery recording per line, a
  payments grid with an inline form to add one, status changes, and cancellation that first shows
  exactly which payments will be refunded and the total before you confirm.
- **Payments screen** — its own page, not a panel on the order: orders with a balance, an amount
  pre-filled with the balance, part payments, and the day's collections listed below. An order stays
  open until it is fully paid.
- **Reports** — a daily screen with three views of one date: orders raised, deliveries made, money
  received, each grouped by its own date and each linking back to the order.
- **SQLite persistence** — schema applied on first start, WAL, foreign keys on. `IDataStore` keeps
  an in-memory implementation for tests and throwaway runs.

## Scope decisions taken

These were settled deliberately and the code follows them:

- No draft state. An order is created with its number and status; there is no confirm step.
- No repair, alteration or remake order types. New orders only.
- No customer-supplied metal.
- No karigar or artisan tracking.
- No metal rate master. The rate is typed per line, with the date stored for reference.
- No payment allocation table. A payment names one order.
- No credit notes or post-delivery returns. Cancelling before delivery refunds in full.
- Tax is four independent components — gold, wastage, making, stone — each optional, each restricted
  to 3, 5, 9, 12, 18 or 28 percent. Unchecked means untaxed.

One consequence worth knowing: **other charges are not taxed**, because the four components have no
box for them. That is deliberate — confirmed with the shop, not an oversight.

## Prerequisites

- .NET SDK 10.0 or later
- Node.js 20.19+ (Angular 20). Node 22 LTS is recommended.
- Optionally Docker.

## Run it

```bash
# terminal 1 - API on http://localhost:5080
cd src/api
dotnet run

# terminal 2 - web on http://localhost:4200
cd src/web
npm install
npm start
```

Pricing checks, which run with no NuGet restore:

```bash
dotnet run --project tests/JewelleryOrderbook.Checks
```

## Database

SQLite, one file at `src/api/data/orderbook.db`, created and migrated on first start. The schema
ships inside the assembly as an embedded resource, so the app can bootstrap an empty file by itself.

| Script | Purpose |
| --- | --- |
| `db/schema.sql` | Tables, indexes, foreign keys and CHECK constraints. Idempotent. |
| `db/views.sql` | `v_order_totals`, `v_order_summary`, the three daily report views and delivery progress. |
| `db/seed-dev.sql` | One customer and one order, for a fresh development database. |

Applying them by hand, or onto an existing file:

```bash
sqlite3 src/api/data/orderbook.db < db/schema.sql
sqlite3 src/api/data/orderbook.db < db/views.sql
sqlite3 src/api/data/orderbook.db < db/seed-dev.sql   # development only
```

Switch to the in-memory store with `Database:Mode=Memory`. The file location and an optional
passphrase are under `Database` in `appsettings.json`.

**Encryption.** The file is plaintext SQLite today. The LLD calls for SQLCipher: swap
`Microsoft.Data.Sqlite` for `Microsoft.Data.Sqlite.Core` plus `SQLitePCLRaw.bundle_e_sqlcipher` and
set `Database:Password`. The `PRAGMA key` is already issued on every connection when a password is
set, and is silently ignored by a non-cipher build, so nothing else changes.

## Identity

OAuth 2.0 with Entra ID. The Angular app is a **public client** using authorization code + PKCE —
no client secret anywhere in this repository, and there never should be. The API is a **resource
server** that validates the bearer token locally against the tenant's published signing keys.

Both sides stay dormant until configured, so the app still runs locally with no Azure account.

### What to collect from Azure

| # | Value | Where in the portal | Goes into |
|---|---|---|---|
| 1 | **Tenant ID** (GUID) | Entra ID → Overview → Tenant ID | `Entra:TenantId` (API), `entraConfig.tenantId` (web) |
| 2 | **SPA client ID** | App registrations → the SPA app → Overview → Application (client) ID | `entraConfig.clientId` (web) |
| 3 | **API client ID** | App registrations → the API app → Overview → Application (client) ID | `entraConfig.apiClientId` (web) |
| 4 | **Application ID URI** | API app → Expose an API → Application ID URI (defaults to `api://<api client id>`) | `Entra:Audience` (API) |
| 5 | **Scope name** | API app → Expose an API → Scopes — the full string `api://<api client id>/access_as_user` | `entraConfig.apiScope` (web) |
| 6 | **Redirect URIs** | SPA app → Authentication → Single-page application | see below |
| 7 | **App role names** | API app → App roles — must be exactly `Owner`, `Accountant`, `Viewer` | matched by the API policies |

### Portal steps

1. **Create the API registration.** App registrations → New. Single tenant. Then *Expose an API* →
   set the Application ID URI to `api://<api-client-id>` → Add a scope called `access_as_user`
   (admin consent display name "Access the order book") → *App roles* → create three roles with the
   values `Owner`, `Accountant`, `Viewer`, allowed member type **Users/Groups**.
2. **Create the SPA registration.** App registrations → New. Single tenant. *Authentication* → Add a
   platform → **Single-page application** → redirect URI `http://localhost:4200` for development, and
   `https://<your-tailnet-name>.ts.net` for the phone. Do **not** add a client secret.
3. **Grant the SPA access to the API.** SPA app → *API permissions* → Add a permission → My APIs →
   the API app → delegated → `access_as_user` → Grant admin consent.
4. **Assign people to roles.** Enterprise applications → the API app → Users and groups → add each
   user and pick their role. Without an assignment the token carries no `roles` claim and every
   request is a 403 even though the signature is valid.
5. **Turn on MFA.** Entra ID → Properties → Manage security defaults → Enabled. That is the free
   tier's MFA; Conditional Access needs P1.

### Filling it in

`src/api/appsettings.Development.json` (or user secrets — better, since this file is committed):

```json
{ "Entra": { "TenantId": "<tenant-guid>", "Audience": "api://<api-client-id>" } }
```

`src/web/src/app/core/auth.config.ts`:

```ts
export const entraConfig: EntraConfig = {
  clientId: '<spa-client-id>',
  tenantId: '<tenant-guid>',
  apiClientId: '<api-client-id>',
  apiScope: 'api://<api-client-id>/access_as_user'
};
```

Leave both blank and: the API authenticates every request as a local `Owner` through
`DevelopmentAuthenticationHandler`, the SPA shows no sign-in button, and the guard lets everything
through. Outside Development the API **refuses to start** without a tenant — that guard is deliberate.

### When it fails

Every rejected token is logged with the exception attached (`JewelleryOrderbook.Auth`). The usual
causes, in order of frequency: the SPA is registered as a *Web* platform instead of *Single-page
application* (produces a CORS error on the token call that looks like a network fault); the audience
is `api://<guid>` but the token was issued for Graph; the user has no app role assigned so `roles` is
empty and a valid token gets a 403; and the redirect URI does not match exactly, including trailing
slashes.

## Conventions

- Money is integer **paise** everywhere. Divided by 100 only when displayed.
- Weight is integer **milligrams**; carats are integer thousandths.
- Percentages are integer **basis points** (800 = 8%).
- Every error carries a stable `code` in the problem details.
- Derived values are computed by `Domain/Pricing.cs` and stored, never entered.

## Layout

```
src/
  api/
    Contracts/       request and response records
    Domain/          entities, enums and the pricing engine
    Endpoints/       route groups, one file per resource
    Extensions/      auth, CORS, helpers
    Infrastructure/  IDataStore and the in-memory implementation
  web/
    src/app/core/      API client and DTOs
    src/app/features/  dashboard, orders, customers
    src/app/shared/    formatting pipes
db/
  schema.sql                   tables, indexes, constraints
  views.sql                    totals and daily report projections
  seed-dev.sql                 one customer and one order
tests/
  JewelleryOrderbook.Checks/   pricing checks
```

## Next

MSAL sign-in, idempotency on order creation, and the backup and purge jobs.

## Licence

Private business application. Public for reference; no warranty.
