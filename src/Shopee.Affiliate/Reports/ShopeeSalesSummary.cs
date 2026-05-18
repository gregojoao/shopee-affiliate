namespace Shopee.Affiliate.Reports;

/// <summary>
/// Aggregate view of the affiliate's performance for a window. Built by paging
/// the underlying <c>conversionReport</c> and reducing the rows client-side.
/// Click-related fields (<see cref="Clicks"/>, <see cref="ConversionRate"/>)
/// are always <c>null</c> because the Affiliate Open API does not expose a
/// click endpoint — see <see cref="ShopeeClickStats"/> for details.
/// </summary>
public sealed record ShopeeSalesSummary(
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    int Conversions,
    int? Clicks,
    Money GrossRevenue,
    Money Commission,
    decimal AvgCommissionRate,
    decimal? ConversionRate,
    IReadOnlyDictionary<ShopeeOrderStatus, int> ByStatus,
    IReadOnlyList<ShopeeTopProduct> TopProducts,
    IReadOnlyList<ShopeeTopShop> TopShops,
    IReadOnlyList<ShopeeTopSubId> TopSubIds,
    bool Supported,
    string? UnsupportedReason);
