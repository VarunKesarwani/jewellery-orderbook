using JewelleryOrderbook.Api.Endpoints;
using JewelleryOrderbook.Api.Extensions;
using JewelleryOrderbook.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Problem details for every unhandled failure, so the client always gets RFC 7807.
builder.Services.AddProblemDetails();

builder.Services.AddCorsForWebClient(builder.Configuration);

// Entra ID. In Development with no tenant configured this falls back to a local identity,
// so the API is runnable before the app registration exists.
builder.AddEntraAuthentication(builder.Configuration);

// Persistence. Sqlite is the real store; Memory exists for tests and for a throwaway run.
var databaseMode = builder.Configuration["Database:Mode"] ?? "Sqlite";

if (databaseMode.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    var sqliteOptions = new SqliteOptions
    {
        Path = builder.Configuration["Database:Path"] ?? "data/orderbook.db",
        Password = builder.Configuration["Database:Password"] ?? string.Empty
    };

    builder.Services.AddSingleton<IDataStore>(_ => new SqliteDataStore(sqliteOptions));
}
else
{
    builder.Services.AddSingleton<IDataStore, InMemoryDataStore>();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(WebClientCors.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapCustomerEndpoints();
app.MapOrderEndpoints();
app.MapDeliveryEndpoints();
app.MapPaymentEndpoints();
app.MapReportEndpoints();
app.MapIdentityEndpoints();

app.Run();
