using JewelleryOrderbook.Api.Domain;

namespace JewelleryOrderbook.Api.Contracts;

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string Phone,
    string? Email,
    string? Gstin,
    bool IsActive)
{
    public static CustomerResponse From(Customer c) =>
        new(c.Id, c.Name, c.Phone, c.Email, c.Gstin, c.IsActive);
}

public sealed record CreateCustomerRequest(
    string Name,
    string? Phone,
    string? Email,
    string? Gstin,
    string? BillingAddress,
    int CreditDays = 0);

public sealed record OrderLineResponse(
    Guid Id,
    string ItemName,
    int Quantity,
    string MetalType,
    string? MetalPurity,
    int GrossWeightMg,
    long RatePerGramPaise,
    long MakingChargesPaise,
    long LineTotalPaise);

public sealed record OrderResponse(
    Guid Id,
    string Number,
    Guid CustomerId,
    string OrderType,
    string Status,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    long TotalPaise,
    IReadOnlyList<OrderLineResponse> Lines)
{
    public static OrderResponse From(Order o) => new(
        o.Id,
        o.Number,
        o.CustomerId,
        o.OrderType.ToString(),
        o.Status.ToString(),
        o.OrderDate,
        o.ExpectedDeliveryDate,
        o.TotalPaise,
        o.Lines.Select(l => new OrderLineResponse(
            l.Id, l.ItemName, l.Quantity, l.MetalType.ToString(), l.MetalPurity,
            l.GrossWeightMg, l.RatePerGramPaise, l.MakingChargesPaise, l.LineTotalPaise)).ToList());
}

public sealed record CreateOrderLineRequest(
    string ItemName,
    string? ItemDescription,
    int Quantity,
    string MetalType,
    string? MetalPurity,
    int GrossWeightMg,
    long RatePerGramPaise,
    long MakingChargesPaise);

public sealed record CreateOrderRequest(
    Guid CustomerId,
    string OrderType,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string? Notes,
    IReadOnlyList<CreateOrderLineRequest> Lines);
