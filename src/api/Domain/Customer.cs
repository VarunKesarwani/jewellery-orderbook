namespace JewelleryOrderbook.Api.Domain;

public sealed class Customer
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Gstin { get; set; }
    public string? BillingAddress { get; set; }
    public int CreditDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
