using JewelleryOrderbook.Api.Domain;
using PaymentStateEnum = JewelleryOrderbook.Api.Domain.PaymentState;

namespace JewelleryOrderbook.Api.Contracts;

// ---------- tax ----------

/// <summary>
/// The four optional tax components, expressed as whole percentages.
/// Null or zero means the box is unchecked and the component is not taxed.
/// </summary>
public sealed record TaxRequest(int? Gold, int? Wastage, int? Making, int? Stone)
{
    public TaxConfig ToConfig() => new(
        Gold is null or 0 ? null : Gold * 100,
        Wastage is null or 0 ? null : Wastage * 100,
        Making is null or 0 ? null : Making * 100,
        Stone is null or 0 ? null : Stone * 100);
}

public sealed record TaxResponse(int? Gold, int? Wastage, int? Making, int? Stone)
{
    public static TaxResponse From(TaxConfig t) => new(
        t.GoldTaxBp / 100, t.WastageTaxBp / 100, t.MakingTaxBp / 100, t.StoneTaxBp / 100);
}

// ---------- customer ----------

public sealed record CustomerResponse(
    Guid Id, string Name, string Phone, string Address, string? Pan,
    string? Email, string? Gstin, int CreditDays, string? Notes, bool IsActive)
{
    public static CustomerResponse From(Customer c) =>
        new(c.Id, c.Name, c.Phone, c.Address, c.Pan, c.Email, c.Gstin, c.CreditDays, c.Notes, c.IsActive);
}

public sealed record CustomerRequest(
    string Name, string Phone, string Address,
    string? Pan, string? Email, string? Gstin, int CreditDays, string? Notes);

// ---------- stones ----------

public sealed record StoneRequest(
    string Category, string? Name, string? OriginType, string? Treatment, string? Shape,
    int StoneCount, int TotalCaratsMilli, string? ColourGrade, string? ClarityGrade,
    string? CertificationLab, string? CertificateNumber, long StoneValuePaise, string? SettingType);

public sealed record StoneResponse(
    Guid Id, string Category, string? Name, string? Shape, int StoneCount, int TotalCaratsMilli,
    string? ColourGrade, string? ClarityGrade, string? CertificationLab, string? CertificateNumber,
    long StoneValuePaise, string? SettingType)
{
    public static StoneResponse From(LineStone s) => new(
        s.Id, s.Category.ToString(), s.Name, s.Shape, s.StoneCount, s.TotalCaratsMilli,
        s.ColourGrade, s.ClarityGrade, s.CertificationLab, s.CertificateNumber,
        s.StoneValuePaise, s.SettingType);
}

// ---------- order lines ----------

public sealed record LineRequest(
    string ItemName, string? ItemDescription, int Quantity,
    string MetalType, string? MetalPurity,
    int GrossWeightMg, long RatePerGramPaise, DateOnly? RateDate,
    int WastagePctBp, string MakingType, long MakingRate,
    long OtherChargesPaise, long DiscountPaise,
    TaxRequest? Tax, IReadOnlyList<StoneRequest>? Stones);

public sealed record LineResponse(
    Guid Id, string ItemName, string? ItemDescription, int Quantity,
    string MetalType, string? MetalPurity,
    int GrossWeightMg, int NetWeightMg, int WastageWeightMg,
    long RatePerGramPaise, DateOnly RateDate,
    long MetalValuePaise, long WastageChargePaise, long MakingChargesPaise, long StoneValuePaise,
    long OtherChargesPaise, long SubtotalPaise, long DiscountPaise,
    TaxResponse Tax, long TaxPaise, long LineTotalPaise,
    int DeliveredPieces, IReadOnlyList<StoneResponse> Stones)
{
    public static LineResponse From(OrderLine l, int deliveredPieces) => new(
        l.Id, l.ItemName, l.ItemDescription, l.Quantity,
        l.MetalType.ToString(), l.MetalPurity,
        l.GrossWeightMg, l.Totals.NetWeightMg, l.Totals.WastageWeightMg,
        l.RatePerGramPaise, l.RateDate,
        l.Totals.MetalValuePaise, l.Totals.WastageChargePaise, l.Totals.MakingChargesPaise, l.Totals.StoneValuePaise,
        l.Totals.OtherChargesPaise, l.Totals.SubtotalPaise, l.Totals.DiscountPaise,
        TaxResponse.From(l.Tax), l.Totals.TaxPaise, l.Totals.LineTotalPaise,
        deliveredPieces, l.Stones.Select(StoneResponse.From).ToList());
}

// ---------- orders ----------

public sealed record OrderRequest(
    Guid CustomerId, DateOnly OrderDate, DateOnly? ExpectedDeliveryDate,
    string? Notes, TaxRequest? Tax, IReadOnlyList<LineRequest> Lines);

public sealed record OrderResponse(
    Guid Id, long Number, Guid CustomerId, string CustomerName,
    DateOnly OrderDate, DateOnly? ExpectedDeliveryDate,
    string Status, string PaymentState, string? Notes, string? CancellationReason,
    long SubtotalPaise, long TaxPaise, long RoundOffPaise, long FinalAmountPaise, long PaidPaise,
    int TotalPieces, int DeliveredPieces,
    IReadOnlyList<LineResponse> Lines)
{
    public static OrderResponse From(Order o, Customer? customer, int deliveredPieces, long paidPaise,
        Func<Guid, int>? deliveredForLine = null)
    {
        var state = paidPaise <= 0
            ? PaymentStateEnum.Unpaid
            : paidPaise >= o.FinalAmountPaise ? PaymentStateEnum.Paid : PaymentStateEnum.PartiallyPaid;

        return new OrderResponse(
            o.Id, o.Number, o.CustomerId, customer?.Name ?? "unknown",
            o.OrderDate, o.ExpectedDeliveryDate,
            o.Status.ToString(), state.ToString(), o.Notes, o.CancellationReason,
            o.SubtotalPaise, o.TaxPaise, o.RoundOffPaise, o.FinalAmountPaise, paidPaise,
            o.TotalPieces, deliveredPieces,
            o.Lines.Select(l => LineResponse.From(l, deliveredForLine?.Invoke(l.Id) ?? 0)).ToList());
    }
}

public sealed record OrderStatusRequest(string Status);

public sealed record CancelOrderRequest(string Reason);

// ---------- delivery ----------

public sealed record DeliveryLineRequest(Guid OrderLineId, int Quantity);

public sealed record DeliveryRequest(
    Guid OrderId, DateOnly DeliveredOn, string? ReceivedBy, string? Notes,
    IReadOnlyList<DeliveryLineRequest> Lines);

public sealed record DeliveryResponse(
    Guid Id, long Number, Guid OrderId, DateOnly DeliveredOn, string? ReceivedBy,
    string? Notes, int TotalPieces, IReadOnlyList<DeliveryLineRequest> Lines)
{
    public static DeliveryResponse From(Delivery d) => new(
        d.Id, d.Number, d.OrderId, d.DeliveredOn, d.ReceivedBy, d.Notes, d.TotalPieces,
        d.Lines.Select(l => new DeliveryLineRequest(l.OrderLineId, l.Quantity)).ToList());
}

// ---------- payment ----------

public sealed record PaymentRequest(
    Guid OrderId, Guid? OrderLineId, long AmountPaise, DateOnly PaidOn, string? Method, string? Reference);

public sealed record PaymentResponse(
    Guid Id, long Number, Guid OrderId, Guid? OrderLineId, long AmountPaise,
    DateOnly PaidOn, string? Method, string? Reference, bool IsRefund)
{
    public static PaymentResponse From(Payment p) => new(
        p.Id, p.Number, p.OrderId, p.OrderLineId, p.AmountPaise, p.PaidOn, p.Method, p.Reference, p.IsRefund);
}
