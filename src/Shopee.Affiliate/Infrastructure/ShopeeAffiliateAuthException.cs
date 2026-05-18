namespace Shopee.Affiliate.Infrastructure;

/// <summary>
/// Thrown when Shopee rejects the request signature or affiliate account state.
/// Maps to Shopee error codes <c>10020</c> (invalid signature/timestamp/credential),
/// <c>10031</c> (access deny), <c>10032</c> (invalid affiliate id),
/// <c>10033</c> (account frozen), <c>10034</c> (blacklisted),
/// <c>10035</c> (no access to Open API).
/// </summary>
public sealed class ShopeeAffiliateAuthException : ShopeeAffiliateException
{
    public ShopeeAffiliateAuthException(
        string message,
        string? code = null,
        IReadOnlyList<string>? path = null,
        string? requestId = null,
        Exception? innerException = null)
        : base(message, code, path, requestId, innerException)
    {
    }
}
