using JewelleryOrderbook.Api.Endpoints;
using JewelleryOrderbook.Api.Extensions;
using JewelleryOrderbook.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Problem details for every unhandled failure, so the client always gets RFC 7807.
builder.Services.AddProblemDetails();

builder.Services.AddCorsForWebClient(builder.Configuration);

// Entra ID. In Development with no tenant configured this is a no-op,
// so the API is runnable before the app registration exists.
builder.AddEntraAuthentication(builder.Configuration);

// In-memory for now. Swap for SQLite in Infrastructure/Persistence.
builder.Services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(WebClientCors.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapCustomerEndpoints();
app.MapOrderEndpoints();
app.MapIdentityEndpoints();

app.Run();
