namespace JewelleryOrderbook.Api.Domain;

public enum OrderStatus
{
    Pending = 0,
    InProgress = 1,
    Ready = 2,
    Delivered = 3,
    Cancelled = 4
}

public enum PaymentState
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2
}

public enum MetalType
{
    Gold = 0,
    Silver = 1,
    Platinum = 2
}

public enum MakingChargeType
{
    PerGram = 0,
    Percent = 1,
    Fixed = 2
}

public enum StoneCategory
{
    Diamond = 0,
    Precious = 1,
    SemiPrecious = 2
}

public sealed class Customer
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Phone { get; set; }
    public required string Address { get; set; }
    public string? Pan { get; set; }
    public string? Email { get; set; }
    public string? Gstin { get; set; }
    public int CreditDays { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
