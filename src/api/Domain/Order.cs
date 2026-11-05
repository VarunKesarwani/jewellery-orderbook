namespace JewelleryOrderbook.Api.Domain;

public enum OrderStatus
{
    Draft = 0,
    Pending = 1,
    InProgress = 2,
    Ready = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum OrderType
{
    New = 0,
    Repair = 1,
    Alteration = 2,
    Remake = 3
}

public enum MetalType
{
    None = 0,
    Gold = 1,
    Silver = 2,
    Platinum = 3
}

public sealed class Order
{
    public required Guid Id { get; init; }
    public string Number { get; set; } = string.Empty;
    public required Guid CustomerId { get; init; }
    public OrderType OrderType { get; set; } = OrderType.New;
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
    public List<OrderLine> Lines { get; init; } = [];

    /// <summary>
    /// Placeholder arithmetic. The real engine lives in Pricing/ and will be replaced
    /// once the exact calculation rules are settled.
    /// </summary>
    public long TotalPaise => Lines.Sum(l => l.LineTotalPaise);
}

public sealed class OrderLine
{
    public required Guid Id { get; init; }
    public required string ItemName { get; set; }
    public string? ItemDescription { get; set; }
    public int Quantity { get; set; } = 1;
    public MetalType MetalType { get; set; } = MetalType.Gold;
    public string? MetalPurity { get; set; }
    public int GrossWeightMg { get; set; }
    public long RatePerGramPaise { get; set; }
    public long MakingChargesPaise { get; set; }

    /// <summary>Placeholder until the pricing engine lands.</summary>
    public long LineTotalPaise => (GrossWeightMg * RatePerGramPaise / 1000) + MakingChargesPaise;
}
