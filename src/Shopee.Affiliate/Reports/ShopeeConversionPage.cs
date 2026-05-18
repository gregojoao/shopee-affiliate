namespace Shopee.Affiliate.Reports;

/// <summary>
/// One page of <see cref="ShopeeConversion"/> rows. Pagination mirrors the
/// Shopee Affiliate Open API: <see cref="NextCursor"/> is the <c>scrollId</c>
/// returned by <c>pageInfo</c> and expires ~30 seconds after issue.
/// </summary>
public sealed record ShopeeConversionPage(
    IReadOnlyList<ShopeeConversion> Items,
    int Page,
    int PageSize,
    int? TotalCount,
    string? NextCursor,
    bool HasMore);
