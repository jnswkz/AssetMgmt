using AssetMgmt.Application.Depreciation;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;

namespace AssetMgmt.Tests;

public class DepreciationServiceTests
{
    [Fact]
    public void StraightLine_ComputesBookValueAndNearEndAlert()
    {
        var asset = Asset(cost: 1200m, acquired: new DateTime(2025, 1, 1));
        var policy = Policy(DepreciationMethod.StraightLine, 15);

        var result = DepreciationService.Calculate(asset, policy, null, new DateTime(2026, 1, 1));

        Assert.Equal(240m, result.BookValue);
        Assert.Equal(3, result.RemainingUsefulLifeMonths);
        Assert.True(result.NearEndOfLife);
        Assert.False(result.FullyDepreciated);
    }

    [Fact]
    public void DecliningBalance_NeverFallsBelowSalvageValue()
    {
        var asset = Asset(cost: 1000m, acquired: new DateTime(2010, 1, 1));
        var policy = Policy(DepreciationMethod.DecliningBalance, 36, 0.4m, 10m);

        var result = DepreciationService.Calculate(asset, policy, null, new DateTime(2026, 1, 1));

        Assert.Equal(100m, result.BookValue);
        Assert.True(result.FullyDepreciated);
        Assert.True(result.NeedsUpgrade);
    }

    [Fact]
    public void LatestLedger_IsExposedWithoutRecalculating()
    {
        var asset = Asset(cost: 1000m, acquired: new DateTime(2025, 1, 1));
        var policy = Policy(DepreciationMethod.StraightLine, 36);
        var ledger = new DepreciationLedger
        {
            PeriodDate = new DateTime(2026, 6, 1),
            ClosingBookValue = 525m,
            AccumulatedDepreciation = 475m
        };

        var result = DepreciationService.Calculate(asset, policy, ledger, new DateTime(2026, 7, 1));

        Assert.Equal(525m, result.BookValue);
        Assert.Equal(475m, result.AccumulatedDepreciation);
        Assert.True(result.IsLedgerValue);
        Assert.Equal(ledger.PeriodDate, result.AsOfDate);
    }

    private static AssetInstance Asset(decimal cost, DateTime acquired) => new()
    {
        Id = Guid.NewGuid(), AssetCode = "IT-LAP-0001", AcquisitionCost = cost,
        AcquisitionDate = acquired, SalvageValue = 0m
    };

    private static DepreciationPolicy Policy(
        DepreciationMethod method, int months, decimal? rate = null, decimal salvagePercent = 0m) => new()
    {
        Id = Guid.NewGuid(), Method = method, UsefulLifeMonths = months,
        AnnualDeclineRate = rate, SalvageValuePercent = salvagePercent,
        EffectiveFrom = new DateTime(2000, 1, 1)
    };
}
