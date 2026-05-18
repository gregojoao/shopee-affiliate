namespace Shopee.Affiliate.Infrastructure;

/// <summary>
/// Generic Shopee Affiliate API failure. The reporting client throws more
/// specific subclasses (<see cref="ShopeeAffiliateAuthException"/>,
/// <see cref="ShopeeAffiliateRateLimitException"/>,
/// <see cref="ShopeeAffiliateNotFoundException"/>,
/// <see cref="ShopeeAffiliateUnsupportedException"/>) when the GraphQL error
/// code maps to a known category; everything else surfaces as this exception.
/// </summary>
public class ShopeeAffiliateApiException : ShopeeAffiliateException
{
    public ShopeeAffiliateApiException(string message)
        : base(message)
    {
    }

    public ShopeeAffiliateApiException(string message, Exception innerException)
        : base(message, innerException: innerException)
    {
    }

    public ShopeeAffiliateApiException(
        string message,
        string? code,
        IReadOnlyList<string>? path = null,
        string? requestId = null,
        Exception? innerException = null)
        : base(message, code, path, requestId, innerException)
    {
    }
}
