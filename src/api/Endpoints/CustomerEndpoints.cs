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

        group.MapGet("/", (string? search, IDataStore store) =>
            {
                var query = store.Customers.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(c =>
                        c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || c.Phone.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || (c.Pan?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                return Results.Ok(query.OrderBy(c => c.Name).Select(CustomerResponse.From));
            })
            .WithName("ListCustomers");

        group.MapGet("/{id:guid}", (Guid id, IDataStore store) =>
                store.GetCustomer(id) is { } customer
                    ? Results.Ok(CustomerResponse.From(customer))
                    : ApiResults.Problem("customer.not_found", StatusCodes.Status404NotFound))
            .WithName("GetCustomer");

        group.MapPost("/", (CustomerRequest request, IDataStore store) =>
            {
                var error = Validate(request);
                if (error is not null)
                {
                    return error;
                }

                var customer = store.AddCustomer(new Customer
                {
                    Id = Guid.CreateVersion7(),
                    Name = request.Name.Trim(),
                    Phone = request.Phone.Trim(),
                    Address = request.Address.Trim(),
                    Pan = request.Pan,
                    Email = request.Email,
                    Gstin = request.Gstin,
                    CreditDays = request.CreditDays,
                    Notes = request.Notes
                });

                return Results.Created($"/api/customers/{customer.Id}", CustomerResponse.From(customer));
            })
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("CreateCustomer");

        group.MapPut("/{id:guid}", (Guid id, CustomerRequest request, IDataStore store) =>
            {
                var error = Validate(request);
                if (error is not null)
                {
                    return error;
                }

                if (store.GetCustomer(id) is not { } existing)
                {
                    return ApiResults.Problem("customer.not_found", StatusCodes.Status404NotFound);
                }

                existing.Name = request.Name.Trim();
                existing.Phone = request.Phone.Trim();
                existing.Address = request.Address.Trim();
                existing.Pan = request.Pan;
                existing.Email = request.Email;
                existing.Gstin = request.Gstin;
                existing.CreditDays = request.CreditDays;
                existing.Notes = request.Notes;

                store.UpdateCustomer(existing);
                return Results.Ok(CustomerResponse.From(existing));
            })
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("UpdateCustomer");

        return app;
    }

    private static IResult? Validate(CustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResults.Problem("customer.name_required", StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return ApiResults.Problem("customer.phone_required", StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return ApiResults.Problem("customer.address_required", StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrWhiteSpace(request.Pan) && request.Pan.Trim().Length != 10)
        {
            return ApiResults.Problem("customer.pan_invalid", StatusCodes.Status400BadRequest,
                "PAN must be ten characters.");
        }

        return null;
    }
}
