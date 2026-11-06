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

// In memory for now. Swap for SQLite behind the same IDataStore interface.
builder.Services.AddSingleton<IDataStore, InMemoryDataStore>();

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
