namespace Shopee.Affiliate.Reports;

/// <summary>
/// Inputs for <see cref="IShopeeAffiliateReportsClient.GetSalesSummaryAsync"/>.
/// The summary is aggregated client-side by pulling every page of the
/// underlying <c>conversionReport</c>; keep the window tight on chatty AppIds.
/// </summary>
public sealed record ShopeeSalesSummaryRequest(
    DateTimeOffset From,
    DateTimeOffset To,
    string? SubId = null);
