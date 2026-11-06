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
- **Order entry screen** — customer picker, metal block, stones, the four tax boxes, and live
  totals priced by the server on every keystroke (`POST /api/orders/calculate`), so the preview
  cannot drift from what gets stored.
- **Order detail** — items with delivered and remaining counts, delivery recording per line, payment
  recording, status changes, and cancellation with refund. Delivery and payment are panels on the
  order rather than separate screens.
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
box for them. If you need them taxed, say so and they join the stone component or get a fifth box.

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

The API is an OAuth resource server for Entra ID, mapping app roles to `Owner`, `Accountant` and
`Viewer` policies. Set `Entra:TenantId` and `Entra:Audience` in `src/api/appsettings.Development.json`
to turn real validation on.

With them blank and `ASPNETCORE_ENVIRONMENT=Development`, every request is authenticated as a local
`Owner` through `DevelopmentAuthenticationHandler`. Outside Development the API refuses to start
without a tenant. The Angular side will use `@azure/msal-angular` with authorization code + PKCE;
there is no client secret anywhere in this repository and there never should be.

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

MSAL sign-in and the backup and purge jobs.

## Licence

Private business application. Public for reference; no warranty.
