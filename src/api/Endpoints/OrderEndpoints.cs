using JewelleryOrderbook.Api.Contracts;
using JewelleryOrderbook.Api.Domain;
using JewelleryOrderbook.Api.Extensions;
using JewelleryOrderbook.Api.Infrastructure;

namespace JewelleryOrderbook.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders")
            .WithTags("orders")
            .RequireAuthorization(AuthorizationPolicies.Read);

        group.MapGet("/", (OrderStatus? status, IOrderRepository repo) =>
                Results.Ok(repo.List(status).Select(OrderResponse.From)))
            .WithName("ListOrders");

        group.MapGet("/{id:guid}", (Guid id, IOrderRepository repo) =>
                repo.Get(id) is { } order
                    ? Results.Ok(OrderResponse.From(order))
                    : ApiResults.Problem("order.not_found", StatusCodes.Status404NotFound))
            .WithName("GetOrder");

        group.MapPost("/", (CreateOrderRequest request, IOrderRepository repo) =>
            {
                if (request.Lines.Count == 0)
                {
                    return ApiResults.Problem("order.no_lines", StatusCodes.Status400BadRequest);
                }

                if (!Enum.TryParse<OrderType>(request.OrderType, ignoreCase: true, out var orderType))
                {
                    return ApiResults.Problem("order.type_invalid", StatusCodes.Status400BadRequest);
                }

                var order = new Order
                {
                    Id = Guid.CreateVersion7(),
                    CustomerId = request.CustomerId,
                    OrderType = orderType,
                    Status = OrderStatus.Draft,
                    OrderDate = request.OrderDate,
                    ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                    Notes = request.Notes
                };

                foreach (var line in request.Lines)
                {
                    order.Lines.Add(new OrderLine
                    {
                        Id = Guid.CreateVersion7(),
                        ItemName = line.ItemName,
                        ItemDescription = line.ItemDescription,
                        Quantity = line.Quantity,
                        MetalType = Enum.TryParse<MetalType>(line.MetalType, ignoreCase: true, out var metal)
                            ? metal
                            : MetalType.Gold,
                        MetalPurity = line.MetalPurity,
                        GrossWeightMg = line.GrossWeightMg,
                        RatePerGramPaise = line.RatePerGramPaise,
                        MakingChargesPaise = line.MakingChargesPaise
                    });
                }

                repo.Add(order);
                return Results.Created($"/api/orders/{order.Id}", OrderResponse.From(order));
            })
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("CreateOrder");

        return app;
    }
}
