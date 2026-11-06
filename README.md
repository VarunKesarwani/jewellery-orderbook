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
- **Reports** — three daily ones: orders, deliveries, payments.

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
tests/
  JewelleryOrderbook.Checks/   pricing checks
```

## Next

SQLite persistence behind `IDataStore`, the order entry screen, delivery and payment screens, MSAL
sign-in, and the backup and purge jobs.

## Licence

Private business application. Public for reference; no warranty.
