using Shopee.Affiliate.Reports;

namespace Shopee.Affiliate.Infrastructure.Reports;

/// <summary>
/// Folds a list of <see cref="ShopeeConversion"/> rows into a
/// <see cref="ShopeeSalesSummary"/>. Pure function; isolated from HTTP so the
/// reporting client stays focused on transport.
/// </summary>
internal static class ShopeeSalesSummaryAggregator
{
    private const int TopListSize = 10;

    public static ShopeeSalesSummary Aggregate(
        IReadOnlyList<ShopeeConversion> conversions,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        if (conversions.Count == 0)
        {
            return Empty(from, to);
        }

        var currency = conversions[0].Currency ?? "BRL";
        var gross = 0m;
        var commission = 0m;
        var rateSum = 0m;
        var rateCount = 0;
        var byStatus = new Dictionary<ShopeeOrderStatus, int>();
        var productAgg = new Dictionary<string, ProductAccumulator>();
        var shopAgg = new Dictionary<string, ShopAccumulator>();
        var subAgg = new Dictionary<string, SubIdAccumulator>();

        foreach (var c in conversions)
        {
            gross += c.TotalSale.Amount;
            commission += c.Commission.Amount;
            if (c.CommissionRate > 0)
            {
                rateSum += c.CommissionRate;
                rateCount++;
            }

            byStatus[c.Status] = byStatus.TryGetValue(c.Status, out var statusCount) ? statusCount + 1 : 1;

            if (!string.IsNullOrWhiteSpace(c.ProductId))
            {
                var prev = productAgg.TryGetValue(c.ProductId!, out var p) ? p : new ProductAccumulator();
                productAgg[c.ProductId!] = prev.Add(c.ProductTitle, c.Commission.Amount);
            }

            if (!string.IsNullOrWhiteSpace(c.ShopId))
            {
                var prev = shopAgg.TryGetValue(c.ShopId!, out var s) ? s : new ShopAccumulator();
                shopAgg[c.ShopId!] = prev.Add(c.ShopName, c.Commission.Amount);
            }

            foreach (var sub in c.SubIds)
            {
                var prev = subAgg.TryGetValue(sub, out var sp) ? sp : new SubIdAccumulator();
                subAgg[sub] = prev.Add(c.Commission.Amount);
            }
        }

        var topProducts = productAgg
            .OrderByDescending(p => p.Value.Commission)
            .Take(TopListSize)
            .Select(p => new ShopeeTopProduct(p.Key, p.Value.Title, p.Value.Conversions, new Money(p.Value.Commission, currency)))
            .ToList();

        var topShops = shopAgg
            .OrderByDescending(p => p.Value.Commission)
            .Take(TopListSize)
            .Select(p => new ShopeeTopShop(p.Key, p.Value.Name, p.Value.Conversions, new Money(p.Value.Commission, currency)))
            .ToList();

        var topSubIds = subAgg
            .OrderByDescending(p => p.Value.Commission)
            .Take(TopListSize)
            .Select(p => new ShopeeTopSubId(p.Key, p.Value.Conversions, new Money(p.Value.Commission, currency)))
            .ToList();

        var avgRate = rateCount > 0 ? Math.Round(rateSum / rateCount, 4, MidpointRounding.AwayFromZero) : 0m;

        return new ShopeeSalesSummary(
            PeriodStart: from,
            PeriodEnd: to,
            Conversions: conversions.Count,
            Clicks: null,
            GrossRevenue: new Money(gross, currency),
            Commission: new Money(commission, currency),
            AvgCommissionRate: avgRate,
            ConversionRate: null,
            ByStatus: byStatus,
            TopProducts: topProducts,
            TopShops: topShops,
            TopSubIds: topSubIds,
            Supported: true,
            UnsupportedReason: null);
    }

    private static ShopeeSalesSummary Empty(DateTimeOffset from, DateTimeOffset to)
        => new(
            PeriodStart: from,
            PeriodEnd: to,
            Conversions: 0,
            Clicks: null,
            GrossRevenue: Money.Zero(),
            Commission: Money.Zero(),
            AvgCommissionRate: 0m,
            ConversionRate: null,
            ByStatus: new Dictionary<ShopeeOrderStatus, int>(),
            TopProducts: Array.Empty<ShopeeTopProduct>(),
            TopShops: Array.Empty<ShopeeTopShop>(),
            TopSubIds: Array.Empty<ShopeeTopSubId>(),
            Supported: true,
            UnsupportedReason: null);

    private readonly record struct ProductAccumulator(string? Title, int Conversions, decimal Commission)
    {
        public ProductAccumulator Add(string? title, decimal amount)
            => new(Title ?? title, Conversions + 1, Commission + amount);
    }

    private readonly record struct ShopAccumulator(string? Name, int Conversions, decimal Commission)
    {
        public ShopAccumulator Add(string? name, decimal amount)
            => new(Name ?? name, Conversions + 1, Commission + amount);
    }

    private readonly record struct SubIdAccumulator(int Conversions, decimal Commission)
    {
        public SubIdAccumulator Add(decimal amount) => new(Conversions + 1, Commission + amount);
    }
}
