namespace Shopee.Affiliate.Reports;

/// <summary>
/// Aggregate view of generated-link usage. The Shopee Affiliate Open API does
/// not currently expose link-generation counts or click attribution, so this
/// type is returned with <c>Supported=false</c> and zeroed counters.
/// </summary>
public sealed record ShopeeLinkUsage(
    int LinksGenerated,
    int ClicksAttributed,
    int ConversionsAttributed,
    Money CommissionAttributed,
    bool Supported,
    string? UnsupportedReason);
