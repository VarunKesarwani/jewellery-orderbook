using JewelleryOrderbook.Api.Extensions;
using JewelleryOrderbook.Api.Infrastructure;

namespace JewelleryOrderbook.Api.Endpoints;

/// <summary>
/// Three daily reports: what was ordered, what went out, what came in.
/// Each groups by its own natural date, which is why they will not tie out against each other.
/// </summary>
public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports")
            .WithTags("reports")
            .RequireAuthorization(AuthorizationPolicies.Read);

        group.MapGet("/daily/orders", (DateOnly? date, IDataStore store) =>
            {
                var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var rows = store.Orders
                    .Where(o => o.OrderDate == day && o.Status != Domain.OrderStatus.Cancelled)
                    .OrderBy(o => o.Number)
                    .Select(o => new
                    {
                        id = o.Id,
                        number = o.Number,
                        customer = store.GetCustomer(o.CustomerId)?.Name,
                        pieces = o.TotalPieces,
                        subtotalPaise = o.SubtotalPaise,
                        taxPaise = o.TaxPaise,
                        finalAmountPaise = o.FinalAmountPaise
                    })
                    .ToList();

                return Results.Ok(new
                {
                    date = day,
                    orderCount = rows.Count,
                    pieces = rows.Sum(r => r.pieces),
                    valuePaise = rows.Sum(r => r.finalAmountPaise),
                    taxPaise = rows.Sum(r => r.taxPaise),
                    rows
                });
            })
            .WithName("DailyOrders");

        group.MapGet("/daily/deliveries", (DateOnly? date, IDataStore store) =>
            {
                var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var rows = store.Deliveries
                    .Where(d => d.DeliveredOn == day)
                    .OrderBy(d => d.Number)
                    .Select(d => new
                    {
                        id = d.Id,
                        orderId = d.OrderId,
                        number = d.Number,
                        orderNumber = store.GetOrder(d.OrderId)?.Number,
                        customer = store.GetCustomer(store.GetOrder(d.OrderId)?.CustomerId ?? Guid.Empty)?.Name,
                        pieces = d.TotalPieces,
                        receivedBy = d.ReceivedBy
                    })
                    .ToList();

                return Results.Ok(new
                {
                    date = day,
                    deliveryCount = rows.Count,
                    pieces = rows.Sum(r => r.pieces),
                    rows
                });
            })
            .WithName("DailyDeliveries");

        group.MapGet("/daily/payments", (DateOnly? date, IDataStore store) =>
            {
                var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var rows = store.Payments
                    .Where(p => p.PaidOn == day)
                    .OrderBy(p => p.Number)
                    .Select(p => new
                    {
                        id = p.Id,
                        orderId = p.OrderId,
                        number = p.Number,
                        orderNumber = store.GetOrder(p.OrderId)?.Number,
                        amountPaise = p.IsRefund ? -p.AmountPaise : p.AmountPaise,
                        method = p.Method,
                        reference = p.Reference,
                        isRefund = p.IsRefund
                    })
                    .ToList();

                return Results.Ok(new
                {
                    date = day,
                    paymentCount = rows.Count,
                    collectedPaise = rows.Where(r => !r.isRefund).Sum(r => r.amountPaise),
                    refundedPaise = -rows.Where(r => r.isRefund).Sum(r => r.amountPaise),
                    netPaise = rows.Sum(r => r.amountPaise),
                    rows
                });
            })
            .WithName("DailyPayments");

        return app;
    }
}
