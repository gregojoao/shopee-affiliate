namespace Shopee.Affiliate.Reports;

/// <summary>
/// Time-bucketed click report. The Shopee Affiliate Open API does not expose a
/// click endpoint today, so the SDK returns
/// <c>Supported=false</c> with an empty <see cref="Points"/> list. The shape is
/// stable so callers can adopt the metric without code changes once Shopee
/// publishes the query.
/// </summary>
public sealed record ShopeeClickStats(
    ShopeeReportGranularity Granularity,
    IReadOnlyList<ShopeeClickPoint> Points,
    bool Supported,
    string? UnsupportedReason);
