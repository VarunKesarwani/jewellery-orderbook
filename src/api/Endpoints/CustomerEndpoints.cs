using JewelleryOrderbook.Api.Contracts;
using JewelleryOrderbook.Api.Domain;
using JewelleryOrderbook.Api.Extensions;
using JewelleryOrderbook.Api.Infrastructure;

namespace JewelleryOrderbook.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("customers")
            .RequireAuthorization(AuthorizationPolicies.Read);

        group.MapGet("/", (string? search, ICustomerRepository repo) =>
                Results.Ok(repo.List(search).Select(CustomerResponse.From)))
            .WithName("ListCustomers");

        group.MapGet("/{id:guid}", (Guid id, ICustomerRepository repo) =>
                repo.Get(id) is { } customer
                    ? Results.Ok(CustomerResponse.From(customer))
                    : ApiResults.Problem("customer.not_found", StatusCodes.Status404NotFound))
            .WithName("GetCustomer");

        group.MapPost("/", (CreateCustomerRequest request, ICustomerRepository repo) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return ApiResults.Problem("customer.name_required", StatusCodes.Status400BadRequest);
                }

                var customer = repo.Add(new Customer
                {
                    Id = Guid.CreateVersion7(),
                    Name = request.Name.Trim(),
                    Phone = request.Phone?.Trim() ?? string.Empty,
                    Email = request.Email,
                    Gstin = request.Gstin,
                    BillingAddress = request.BillingAddress,
                    CreditDays = request.CreditDays
                });

                return Results.Created($"/api/customers/{customer.Id}", CustomerResponse.From(customer));
            })
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("CreateCustomer");

        return app;
    }
}
