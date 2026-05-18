namespace Shopee.Affiliate.Reports;

/// <summary>
/// Inputs for <see cref="IShopeeAffiliateReportsClient.GetClickStatsAsync"/>.
/// As of the latest public Affiliate Open API the click endpoint is not
/// exposed, so the response is always <c>Supported=false</c>; the request type
/// remains stable so the consumer code does not change once Shopee adds the
/// query.
/// </summary>
public sealed record ShopeeClickStatsRequest(
    DateTimeOffset From,
    DateTimeOffset To,
    ShopeeReportGranularity Granularity = ShopeeReportGranularity.Day,
    string? SubId = null);
