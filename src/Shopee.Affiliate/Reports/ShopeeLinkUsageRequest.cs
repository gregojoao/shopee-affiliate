namespace Shopee.Affiliate.Reports;

/// <summary>
/// Inputs for <see cref="IShopeeAffiliateReportsClient.GetGeneratedLinkUsageAsync"/>.
/// Shopee Affiliate Open API does not expose a count of generated short links,
/// so the response is always <c>Supported=false</c>. The request type remains
/// stable so the caller code keeps working when Shopee publishes the metric.
/// </summary>
public sealed record ShopeeLinkUsageRequest(
    DateTimeOffset From,
    DateTimeOffset To,
    string? SubId = null);
