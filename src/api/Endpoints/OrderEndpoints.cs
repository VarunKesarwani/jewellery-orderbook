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

        group.MapGet("/", (Guid? customerId, OrderStatus? status, DateOnly? from, DateOnly? to,
                          string? search, IDataStore store) =>
            {
                var query = store.Orders.AsEnumerable();

                if (customerId is not null)
                {
                    query = query.Where(o => o.CustomerId == customerId);
                }

                if (status is not null)
                {
                    query = query.Where(o => o.Status == status);
                }

                if (from is not null)
                {
                    query = query.Where(o => o.OrderDate >= from);
                }

                if (to is not null)
                {
                    query = query.Where(o => o.OrderDate <= to);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(o =>
                        o.Number.ToString().Contains(search)
                        || o.Lines.Any(l => l.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase))
                        || (store.GetCustomer(o.CustomerId)?.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                return Results.Ok(query
                    .OrderByDescending(o => o.Number)
                    .Select(o => OrderResponse.From(o, store.GetCustomer(o.CustomerId),
                        store.DeliveredPieces(o.Id), store.PaidPaise(o.Id))));
            })
            .WithName("SearchOrders");

        group.MapGet("/{id:guid}", (Guid id, IDataStore store) =>
                store.GetOrder(id) is { } order
                    ? Results.Ok(OrderResponse.From(order, store.GetCustomer(order.CustomerId),
                        store.DeliveredPieces(order.Id), store.PaidPaise(order.Id)))
                    : ApiResults.Problem("order.not_found", StatusCodes.Status404NotFound))
            .WithName("GetOrder");

        group.MapPost("/", (OrderRequest request, IDataStore store) =>
            {
                if (store.GetCustomer(request.CustomerId) is null)
                {
                    return ApiResults.Problem("customer.not_found", StatusCodes.Status400BadRequest);
                }

                if (request.Lines is null || request.Lines.Count == 0)
                {
                    return ApiResults.Problem("order.no_lines", StatusCodes.Status400BadRequest);
                }

                var orderTax = (request.Tax ?? new TaxRequest(null, null, null, null)).ToConfig();
                if (!orderTax.IsValid())
                {
                    return ApiResults.Problem("tax.rate_invalid", StatusCodes.Status400BadRequest,
                        "Allowed rates are 3, 5, 9, 12, 18 and 28 percent.");
                }

                var order = new Order
                {
                    Id = Guid.CreateVersion7(),
                    Number = store.NextOrderNumber(),
                    CustomerId = request.CustomerId,
                    OrderDate = request.OrderDate,
                    ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                    Notes = request.Notes,
                    Status = OrderStatus.Pending
                };

                foreach (var lineRequest in request.Lines)
                {
                    if (lineRequest.GrossWeightMg <= 0)
                    {
                        return ApiResults.Problem("line.weight_invalid", StatusCodes.Status400BadRequest);
                    }

                    var tax = (lineRequest.Tax ?? request.Tax ?? new TaxRequest(null, null, null, null)).ToConfig();
                    if (!tax.IsValid())
                    {
                        return ApiResults.Problem("tax.rate_invalid", StatusCodes.Status400BadRequest,
                            "Allowed rates are 3, 5, 9, 12, 18 and 28 percent.");
                    }

                    var line = new OrderLine
                    {
                        Id = Guid.CreateVersion7(),
                        ItemName = lineRequest.ItemName,
                        ItemDescription = lineRequest.ItemDescription,
                        Quantity = Math.Max(1, lineRequest.Quantity),
                        MetalType = Enum.TryParse<MetalType>(lineRequest.MetalType, true, out var metal) ? metal : MetalType.Gold,
                        MetalPurity = lineRequest.MetalPurity,
                        GrossWeightMg = lineRequest.GrossWeightMg,
                        RatePerGramPaise = lineRequest.RatePerGramPaise,
                        RateDate = lineRequest.RateDate ?? request.OrderDate,
                        WastagePctBp = lineRequest.WastagePctBp,
                        MakingType = Enum.TryParse<MakingChargeType>(lineRequest.MakingType, true, out var making) ? making : MakingChargeType.PerGram,
                        MakingRate = lineRequest.MakingRate,
                        OtherChargesPaise = lineRequest.OtherChargesPaise,
                        DiscountPaise = lineRequest.DiscountPaise,
                        Tax = tax
                    };

                    foreach (var stone in lineRequest.Stones ?? [])
                    {
                        line.Stones.Add(new LineStone
                        {
                            Id = Guid.CreateVersion7(),
                            Category = Enum.TryParse<StoneCategory>(stone.Category, true, out var category) ? category : StoneCategory.Diamond,
                            Name = stone.Name,
                            OriginType = stone.OriginType,
                            Treatment = stone.Treatment,
                            Shape = stone.Shape,
                            StoneCount = Math.Max(1, stone.StoneCount),
                            TotalCaratsMilli = stone.TotalCaratsMilli,
                            ColourGrade = stone.ColourGrade,
                            ClarityGrade = stone.ClarityGrade,
                            CertificationLab = stone.CertificationLab,
                            CertificateNumber = stone.CertificateNumber,
                            StoneValuePaise = stone.StoneValuePaise,
                            SettingType = stone.SettingType
                        });
                    }

                    line.Totals = Pricing.ComputeLine(line);
                    order.Lines.Add(line);
                }

                store.AddOrder(order);

                return Results.Created($"/api/orders/{order.Id}",
                    OrderResponse.From(order, store.GetCustomer(order.CustomerId), 0, 0));
            })
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("CreateOrder");

        group.MapPost("/{id:guid}/status", (Guid id, OrderStatusRequest request, IDataStore store) =>
            {
                if (store.GetOrder(id) is not { } order)
                {
                    return ApiResults.Problem("order.not_found", StatusCodes.Status404NotFound);
                }

                if (order.Status is OrderStatus.Cancelled or OrderStatus.Delivered)
                {
                    return ApiResults.Problem("order.closed", StatusCodes.Status409Conflict);
                }

                if (!Enum.TryParse<OrderStatus>(request.Status, true, out var next))
                {
                    return ApiResults.Problem("order.status_invalid", StatusCodes.Status400BadRequest);
                }

                order.Status = next;
                store.UpdateOrder(order);
                return Results.Ok(OrderResponse.From(order, store.GetCustomer(order.CustomerId),
                    store.DeliveredPieces(order.Id), store.PaidPaise(order.Id)));
            })
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("ChangeOrderStatus");

        group.MapPost("/{id:guid}/cancel", (Guid id, CancelOrderRequest request, IDataStore store) =>
            {
                if (store.GetOrder(id) is not { } order)
                {
                    return ApiResults.Problem("order.not_found", StatusCodes.Status404NotFound);
                }

                if (order.Status == OrderStatus.Cancelled)
                {
                    return ApiResults.Problem("order.already_cancelled", StatusCodes.Status409Conflict);
                }

                if (store.DeliveredPieces(order.Id) > 0)
                {
                    return ApiResults.Problem("order.already_delivered", StatusCodes.Status409Conflict,
                        "An order cannot be cancelled once anything has been delivered.");
                }

                // Refund by posting reversing rows; payments are never edited.
                var paid = store.PaidPaise(order.Id);
                if (paid > 0)
                {
                    store.AddPayment(new Payment
                    {
                        Id = Guid.CreateVersion7(),
                        Number = store.NextPaymentNumber(),
                        OrderId = order.Id,
                        AmountPaise = paid,
                        PaidOn = DateOnly.FromDateTime(DateTime.UtcNow),
                        Method = "refund",
                        Reference = request.Reason,
                        IsRefund = true
                    });
                }

                order.Status = OrderStatus.Cancelled;
                order.CancellationReason = request.Reason;
                store.UpdateOrder(order);

                return Results.Ok(OrderResponse.From(order, store.GetCustomer(order.CustomerId),
                    0, store.PaidPaise(order.Id)));
            })
            .RequireAuthorization(AuthorizationPolicies.Write)
            .WithName("CancelOrder");

        return app;
    }
}
