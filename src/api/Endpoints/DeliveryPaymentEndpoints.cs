using JewelleryOrderbook.Api.Contracts;
using JewelleryOrderbook.Api.Domain;
using JewelleryOrderbook.Api.Extensions;
using JewelleryOrderbook.Api.Infrastructure;

namespace JewelleryOrderbook.Api.Endpoints;

public static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deliveries")
            .WithTags("deliveries")
            .RequireAuthorization(AuthorizationPolicies.Read);

        group.MapGet("/", (Guid? orderId, DateOnly? date, IDataStore store) =>
            {
                var query = store.Deliveries.AsEnumerable();
                if (orderId is not null) query = query.Where(d => d.OrderId == orderId);
                if (date is not null) query = query.Where(d => d.DeliveredOn == date);

                return Results.Ok(query.OrderByDescending(d => d.Number).Select(DeliveryResponse.From));
            })
            .WithName("ListDeliveries");

        group.MapPost("/", (DeliveryRequest request, IDataStore store) =>
            {
                if (store.GetOrder(request.OrderId) is not { } order)
                {
                    return ApiResults.Problem("order.not_found", StatusCodes.Status400BadRequest);
                }

                if (order.Status == OrderStatus.Cancelled)
                {
                    return ApiResults.Problem("order.cancelled", StatusCodes.Status409Conflict);
                }

                if (request.Lines is null || request.Lines.Count == 0)
                {
                    return ApiResults.Problem("delivery.no_lines", StatusCodes.Status400BadRequest);
                }

                var delivery = new Delivery
                {
                    Id = Guid.CreateVersion7(),
                    Number = store.NextDeliveryNumber(),
                    OrderId = order.Id,
                    DeliveredOn = request.DeliveredOn,
                    ReceivedBy = request.ReceivedBy,
                    Notes = request.Notes
                };

                foreach (var line in request.Lines)
                {
                    var orderLine = order.Lines.FirstOrDefault(l => l.Id == line.OrderLineId);
                    if (orderLine is null)
                    {
                        return ApiResults.Problem("delivery.line_not_on_order", StatusCodes.Status400BadRequest);
                    }

                    var alreadyDelivered = store.DeliveredPieces(order.Id, line.OrderLineId);
                    var remaining = orderLine.Quantity - alreadyDelivered;

                    if (line.Quantity <= 0 || line.Quantity > remaining)
                    {
                        return ApiResults.Problem("delivery.over_qty", StatusCodes.Status409Conflict,
                            $"{orderLine.ItemName}: {remaining} of {orderLine.Quantity} pieces remain.");
                    }

                    delivery.Lines.Add(new DeliveryLine { OrderLineId = line.OrderLineId, Quantity = line.Quantity });
                }

                store.AddDelivery(delivery);

                if (store.DeliveredPieces(order.Id) >= order.TotalPieces)
                {
                    order.Status = OrderStatus.Delivered;
                    store.UpdateOrder(order);
                }

                return Results.Created($"/api/deliveries/{delivery.Id}", DeliveryResponse.From(delivery));
            })
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("CreateDelivery");

        return app;
    }
}

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments")
            .WithTags("payments")
            .RequireAuthorization(AuthorizationPolicies.Read);

        group.MapGet("/", (Guid? orderId, DateOnly? date, IDataStore store) =>
            {
                var query = store.Payments.AsEnumerable();
                if (orderId is not null) query = query.Where(p => p.OrderId == orderId);
                if (date is not null) query = query.Where(p => p.PaidOn == date);

                return Results.Ok(query.OrderByDescending(p => p.Number).Select(PaymentResponse.From));
            })
            .WithName("ListPayments");

        group.MapPost("/", (PaymentRequest request, IDataStore store) =>
            {
                if (store.GetOrder(request.OrderId) is not { } order)
                {
                    return ApiResults.Problem("order.not_found", StatusCodes.Status400BadRequest);
                }

                if (order.Status == OrderStatus.Cancelled)
                {
                    return ApiResults.Problem("order.cancelled", StatusCodes.Status409Conflict);
                }

                if (request.AmountPaise <= 0)
                {
                    return ApiResults.Problem("payment.amount_invalid", StatusCodes.Status400BadRequest);
                }

                if (request.OrderLineId is not null && order.Lines.All(l => l.Id != request.OrderLineId))
                {
                    return ApiResults.Problem("payment.line_not_on_order", StatusCodes.Status400BadRequest);
                }

                var payment = store.AddPayment(new Payment
                {
                    Id = Guid.CreateVersion7(),
                    Number = store.NextPaymentNumber(),
                    OrderId = order.Id,
                    OrderLineId = request.OrderLineId,
                    AmountPaise = request.AmountPaise,
                    PaidOn = request.PaidOn,
                    Method = request.Method,
                    Reference = request.Reference
                });

                return Results.Created($"/api/payments/{payment.Id}", PaymentResponse.From(payment));
            })
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("RecordPayment");

        return app;
    }
}
