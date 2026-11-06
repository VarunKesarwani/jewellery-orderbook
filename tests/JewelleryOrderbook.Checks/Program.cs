using JewelleryOrderbook.Api.Domain;

var failures = 0;
var checks = 0;

void Check(string name, long actual, long expected)
{
    checks++;
    if (actual == expected)
    {
        Console.WriteLine($"  PASS  {name}  ({actual})");
        return;
    }

    failures++;
    Console.WriteLine($"  FAIL  {name}  expected {expected}, got {actual}");
}

void CheckTrue(string name, bool condition)
{
    checks++;
    if (condition)
    {
        Console.WriteLine($"  PASS  {name}");
        return;
    }

    failures++;
    Console.WriteLine($"  FAIL  {name}");
}

OrderLine Ring(TaxConfig tax, long discount = 100_000, MakingChargeType makingType = MakingChargeType.PerGram,
    long makingRate = 45_000, int wastageBp = 800)
{
    var line = new OrderLine
    {
        Id = Guid.NewGuid(),
        ItemName = "22K ring",
        Quantity = 1,
        MetalType = MetalType.Gold,
        MetalPurity = "22K",
        GrossWeightMg = 8_500,
        RatePerGramPaise = 620_000,
        RateDate = new DateOnly(2026, 11, 6),
        WastagePctBp = wastageBp,
        MakingType = makingType,
        MakingRate = makingRate,
        OtherChargesPaise = 50_000,
        DiscountPaise = discount,
        Tax = tax
    };

    line.Stones.Add(new LineStone
    {
        Id = Guid.NewGuid(),
        Category = StoneCategory.Diamond,
        StoneCount = 3,
        TotalCaratsMilli = 450,
        ColourGrade = "G",
        ClarityGrade = "VVS1",
        CertificationLab = "GIA",
        CertificateNumber = "1234567890",
        StoneValuePaise = 1_575_000,
        SettingType = "prong"
    });

    line.Totals = Pricing.ComputeLine(line);
    return line;
}

Console.WriteLine("Worked example — 22K ring, 3 diamonds, 8% wastage, no tax");

var plain = Ring(TaxConfig.None);
Check("net metal weight (mg)", plain.Totals.NetWeightMg, 8_410);
Check("wastage weight (mg)", plain.Totals.WastageWeightMg, 673);
Check("metal value", plain.Totals.MetalValuePaise, 5_214_200);
Check("wastage charge", plain.Totals.WastageChargePaise, 417_260);
Check("making charges", plain.Totals.MakingChargesPaise, 378_450);
Check("stone value", plain.Totals.StoneValuePaise, 1_575_000);
Check("subtotal", plain.Totals.SubtotalPaise, 7_634_910);
Check("taxable after discount", plain.Totals.TaxablePaise, 7_534_910);
Check("tax with no boxes ticked", plain.Totals.TaxPaise, 0);
Check("line total with no tax", plain.Totals.LineTotalPaise, 7_534_910);

Console.WriteLine();
Console.WriteLine("Wastage billed as weight and as money must agree");
Check("metal plus wastage charge", plain.Totals.MetalValuePaise + plain.Totals.WastageChargePaise, 5_631_460);

Console.WriteLine();
Console.WriteLine("All four components at 3%");

var taxed = Ring(new TaxConfig(300, 300, 300, 300));
Check("gold tax", taxed.Totals.GoldTaxPaise, 154_364);
Check("wastage tax", taxed.Totals.WastageTaxPaise, 12_353);
Check("making tax", taxed.Totals.MakingTaxPaise, 11_204);
Check("stone tax", taxed.Totals.StoneTaxPaise, 46_627);
Check("total tax", taxed.Totals.TaxPaise, 224_548);
Check("line total", taxed.Totals.LineTotalPaise, 7_759_458);
Check("round off to rupee", OrderTotals.RoundOff(taxed.Totals.LineTotalPaise), 42);
Check("final amount", taxed.Totals.LineTotalPaise + OrderTotals.RoundOff(taxed.Totals.LineTotalPaise), 7_759_500);

Console.WriteLine();
Console.WriteLine("Other charges carry no tax component and are not taxed");

var onlyGold = Ring(new TaxConfig(300, null, null, null));
Check("gold-only tax", onlyGold.Totals.TaxPaise, 154_364);
CheckTrue("gold-only tax is less than all four", onlyGold.Totals.TaxPaise < taxed.Totals.TaxPaise);

Console.WriteLine();
Console.WriteLine("Making charge types");

var fixedMaking = Ring(TaxConfig.None, makingType: MakingChargeType.Fixed, makingRate: 500_000);
Check("fixed making charge", fixedMaking.Totals.MakingChargesPaise, 500_000);

var percentMaking = Ring(TaxConfig.None, makingType: MakingChargeType.Percent, makingRate: 1_200);
Check("12% making on metal value", percentMaking.Totals.MakingChargesPaise, 625_704);

Console.WriteLine();
Console.WriteLine("Edge cases");

var noDiscount = Ring(TaxConfig.None, discount: 0);
Check("taxable equals subtotal with no discount", noDiscount.Totals.TaxablePaise, noDiscount.Totals.SubtotalPaise);

var noStones = new OrderLine
{
    Id = Guid.NewGuid(),
    ItemName = "Bangle",
    Quantity = 1,
    GrossWeightMg = 12_000,
    RatePerGramPaise = 620_000,
    RateDate = new DateOnly(2026, 11, 6),
    WastagePctBp = 0,
    MakingType = MakingChargeType.Fixed,
    MakingRate = 0,
    Tax = TaxConfig.None
};
noStones.Totals = Pricing.ComputeLine(noStones);
Check("net weight equals gross with no stones", noStones.Totals.NetWeightMg, 12_000);
Check("metal value only", noStones.Totals.LineTotalPaise, 7_440_000);

CheckTrue("4% is not an allowed tax rate", !new TaxConfig(400, null, null, null).IsValid());
CheckTrue("28% is allowed", new TaxConfig(2800, null, null, null).IsValid());
CheckTrue("zero discount leaves components untouched", Ring(TaxConfig.None, discount: 0).Totals.DiscountPaise == 0);

Console.WriteLine();
Console.WriteLine($"{checks - failures} of {checks} checks passed");

return failures == 0 ? 0 : 1;
