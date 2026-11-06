namespace JewelleryOrderbook.Api.Domain;

/// <summary>
/// The optional tax components. A null rate means that component is not taxed,
/// which is what an unchecked box on the screen means.
/// Allowed rates are the six GST slabs; anything else is rejected at the edge.
/// </summary>
public sealed record TaxConfig(int? GoldTaxBp, int? WastageTaxBp, int? MakingTaxBp, int? StoneTaxBp)
{
    public static readonly TaxConfig None = new(null, null, null, null);

    public static readonly int[] AllowedRatesBp = [300, 500, 900, 1200, 1800, 2800];

    /// <summary>Rates in basis points that the UI may offer, as whole percentages.</summary>
    public static IEnumerable<int> AllowedPercentages => AllowedRatesBp.Select(bp => bp / 100);

    public bool IsValid() =>
        new[] { GoldTaxBp, WastageTaxBp, MakingTaxBp, StoneTaxBp }
            .All(bp => bp is null || AllowedRatesBp.Contains(bp.Value));
}

public sealed class OrderLine
{
    public required Guid Id { get; init; }
    public required string ItemName { get; set; }
    public string? ItemDescription { get; set; }
    public int Quantity { get; set; } = 1;

    public MetalType MetalType { get; set; } = MetalType.Gold;
    public string? MetalPurity { get; set; }

    // Metal and weight, all in milligrams.
    public int GrossWeightMg { get; set; }
    public long RatePerGramPaise { get; set; }
    public DateOnly RateDate { get; set; }

    public int WastagePctBp { get; set; }

    public MakingChargeType MakingType { get; set; } = MakingChargeType.PerGram;
    public long MakingRate { get; set; }

    public long OtherChargesPaise { get; set; }
    public long DiscountPaise { get; set; }

    public TaxConfig Tax { get; set; } = TaxConfig.None;

    public List<LineStone> Stones { get; init; } = [];

    /// <summary>Sum of stone weights, converted from carats at 0.2 g per carat.</summary>
    public int StoneWeightMg => Stones.Sum(s => (int)Math.Round(s.TotalCaratsMilli * 200m / 1000m, MidpointRounding.AwayFromZero));

    public long StoneValuePaise => Stones.Sum(s => s.StoneValuePaise);

    /// <summary>Filled by the pricing engine. Stored, never entered.</summary>
    public LineTotals Totals { get; set; } = LineTotals.Empty;
}

public sealed class LineStone
{
    public required Guid Id { get; init; }
    public StoneCategory Category { get; set; } = StoneCategory.Diamond;
    public string? Name { get; set; }
    public string? OriginType { get; set; }
    public string? Treatment { get; set; }
    public string? Shape { get; set; }
    public int StoneCount { get; set; } = 1;

    /// <summary>Total weight in thousandths of a carat.</summary>
    public int TotalCaratsMilli { get; set; }

    public string? ColourGrade { get; set; }
    public string? ClarityGrade { get; set; }
    public string? CertificationLab { get; set; }
    public string? CertificateNumber { get; set; }

    public long StoneValuePaise { get; set; }
    public string? SettingType { get; set; }
}

public sealed class Order
{
    public required Guid Id { get; init; }
    public required long Number { get; init; }
    public required Guid CustomerId { get; init; }
    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }

    public List<OrderLine> Lines { get; init; } = [];

    public long SubtotalPaise => Lines.Sum(l => l.Totals.SubtotalPaise);
    public long TaxPaise => Lines.Sum(l => l.Totals.TaxPaise);
    public long RoundOffPaise => OrderTotals.RoundOff(Lines.Sum(l => l.Totals.LineTotalPaise));
    public long FinalAmountPaise => Lines.Sum(l => l.Totals.LineTotalPaise) + RoundOffPaise;

    public int TotalPieces => Lines.Sum(l => l.Quantity);
}

public sealed class Delivery
{
    public required Guid Id { get; init; }
    public required long Number { get; init; }
    public required Guid OrderId { get; init; }
    public DateOnly DeliveredOn { get; set; }
    public string? ReceivedBy { get; set; }
    public string? Notes { get; set; }
    public List<DeliveryLine> Lines { get; init; } = [];

    public int TotalPieces => Lines.Sum(l => l.Quantity);
}

public sealed class DeliveryLine
{
    public required Guid OrderLineId { get; init; }
    public int Quantity { get; set; }
}

public sealed class Payment
{
    public required Guid Id { get; init; }
    public required long Number { get; init; }
    public required Guid OrderId { get; init; }

    /// <summary>Set when the payment settles a specific delivered line.</summary>
    public Guid? OrderLineId { get; init; }

    public long AmountPaise { get; set; }
    public DateOnly PaidOn { get; set; }
    public string? Method { get; set; }
    public string? Reference { get; set; }
    public bool IsRefund { get; set; }
}
