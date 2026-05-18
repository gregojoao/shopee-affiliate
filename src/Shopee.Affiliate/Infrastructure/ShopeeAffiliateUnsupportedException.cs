namespace Shopee.Affiliate.Infrastructure;

/// <summary>
/// Thrown when a caller explicitly asks for a metric that the Shopee Affiliate
/// Open API does not expose (for example, click stats or link-usage counts).
/// In the normal flow the reporting client prefers returning <c>Supported=false</c>
/// on the response object; this exception exists for code paths that opt into
/// hard failure instead.
/// </summary>
public sealed class ShopeeAffiliateUnsupportedException : ShopeeAffiliateException
{
    public ShopeeAffiliateUnsupportedException(
        string message,
        string? code = null,
        IReadOnlyList<string>? path = null,
        string? requestId = null,
        Exception? innerException = null)
        : base(message, code, path, requestId, innerException)
    {
    }
}
