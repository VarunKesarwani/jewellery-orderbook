using JewelleryOrderbook.Api.Domain;

namespace JewelleryOrderbook.Api.Domain;

public sealed record LineTotals(
    int NetWeightMg,
    int WastageWeightMg,
    long MetalValuePaise,
    long WastageChargePaise,
    long MakingChargesPaise,
    long StoneValuePaise,
    long OtherChargesPaise,
    long SubtotalPaise,
    long DiscountPaise,
    long TaxablePaise,
    long GoldTaxPaise,
    long WastageTaxPaise,
    long MakingTaxPaise,
    long StoneTaxPaise,
    long TaxPaise,
    long LineTotalPaise)
{
    public static readonly LineTotals Empty =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public static class OrderTotals
{
    /// <summary>Difference between the sum of line totals and the nearest whole rupee.</summary>
    public static long RoundOff(long totalPaise)
    {
        var rounded = (long)Math.Round(totalPaise / 100m, MidpointRounding.AwayFromZero) * 100;
        return rounded - totalPaise;
    }
}

/// <summary>
/// The pricing engine. One pure function per line, one per order, no side effects.
/// Every derived value is rounded to paise and stored, so the printed bill reconciles.
///
/// Order of operations, which matters:
///   net weight -> wastage weight -> metal value -> wastage charge -> making charges
///   -> subtotal -> discount (allocated across taxable components) -> tax per component
///   -> line total.
/// </summary>
public static class Pricing
{
    private const int MgPerGram = 1000;
    private const int BasisPointDivisor = 10_000;

    public static LineTotals ComputeLine(OrderLine line)
    {
        var netMg = Math.Max(0, line.GrossWeightMg - line.StoneWeightMg);
        var wastageMg = (int)Round(netMg * (decimal)line.WastagePctBp / BasisPointDivisor);

        var metalValue = Round(netMg * (decimal)line.RatePerGramPaise / MgPerGram);
        var wastageCharge = Round(wastageMg * (decimal)line.RatePerGramPaise / MgPerGram);

        var making = line.MakingType switch
        {
            MakingChargeType.PerGram => Round(netMg * (decimal)line.MakingRate / MgPerGram),
            MakingChargeType.Percent => Round(metalValue * (decimal)line.MakingRate / BasisPointDivisor),
            _ => line.MakingRate
        };

        var stoneValue = line.StoneValuePaise;
        var other = line.OtherChargesPaise;

        var subtotal = metalValue + wastageCharge + making + stoneValue + other;
        var discount = Math.Clamp(line.DiscountPaise, 0, subtotal);

        // Discount is spread across the four taxable components in proportion to their value,
        // so tax follows the discount instead of being charged on money the customer never paid.
        var (goldBase, wastageBase, makingBase, stoneBase) =
            AllocateDiscount(discount, metalValue, wastageCharge, making, stoneValue);

        var goldTax = ApplyRate(goldBase, line.Tax.GoldTaxBp);
        var wastageTax = ApplyRate(wastageBase, line.Tax.WastageTaxBp);
        var makingTax = ApplyRate(makingBase, line.Tax.MakingTaxBp);
        var stoneTax = ApplyRate(stoneBase, line.Tax.StoneTaxBp);

        var tax = goldTax + wastageTax + makingTax + stoneTax;
        var taxable = subtotal - discount;

        return new LineTotals(
            netMg, wastageMg, metalValue, wastageCharge, making, stoneValue, other,
            subtotal, discount, taxable, goldTax, wastageTax, makingTax, stoneTax, tax,
            taxable + tax);
    }

    private static (long Gold, long Wastage, long Making, long Stone) AllocateDiscount(
        long discount, long gold, long wastage, long making, long stone)
    {
        if (discount <= 0)
        {
            return (gold, wastage, making, stone);
        }

        var total = gold + wastage + making + stone;
        if (total <= 0)
        {
            return (0, 0, 0, 0);
        }

        var capped = Math.Min(discount, total);

        var parts = new[] { gold, wastage, making, stone };
        var allocated = new long[4];
        long running = 0;

        for (var i = 0; i < 4; i++)
        {
            allocated[i] = Round(capped * (decimal)parts[i] / total);
            running += allocated[i];
        }

        // Put the rounding remainder on the largest component so the parts still sum to the discount.
        var remainder = capped - running;
        if (remainder != 0)
        {
            var largest = 0;
            for (var i = 1; i < 4; i++)
            {
                if (parts[i] > parts[largest])
                {
                    largest = i;
                }
            }

            allocated[largest] += remainder;
        }

        return (
            Math.Max(0, parts[0] - allocated[0]),
            Math.Max(0, parts[1] - allocated[1]),
            Math.Max(0, parts[2] - allocated[2]),
            Math.Max(0, parts[3] - allocated[3]));
    }

    private static long ApplyRate(long basePaise, int? rateBp) =>
        rateBp is null or 0 ? 0 : Round(basePaise * (decimal)rateBp.Value / BasisPointDivisor);

    private static long Round(decimal value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);
}
