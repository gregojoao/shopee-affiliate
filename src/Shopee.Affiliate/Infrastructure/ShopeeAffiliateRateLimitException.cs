namespace Shopee.Affiliate.Infrastructure;

/// <summary>
/// Thrown when Shopee returns the rate-limit error (<c>10030</c> — TOO_MANY_REQUESTS).
/// Callers should back off before retrying. Shopee does not publish the exact
/// per-app quota; integrations typically see ~100 requests/minute per AppId.
/// </summary>
public sealed class ShopeeAffiliateRateLimitException : ShopeeAffiliateException
{
    public ShopeeAffiliateRateLimitException(
        string message,
        string? code = null,
        IReadOnlyList<string>? path = null,
        string? requestId = null,
        Exception? innerException = null)
        : base(message, code, path, requestId, innerException)
    {
    }
}
