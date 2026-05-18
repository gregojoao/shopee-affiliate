namespace Shopee.Affiliate.Infrastructure;

/// <summary>
/// Thrown when a single-resource lookup (e.g. <c>GetConversionAsync(orderId)</c>)
/// returns no node. Shopee does not have a dedicated <c>NOT_FOUND</c> code, so
/// the SDK raises this exception when the GraphQL response contains zero rows
/// for an explicit point query.
/// </summary>
public sealed class ShopeeAffiliateNotFoundException : ShopeeAffiliateException
{
    public ShopeeAffiliateNotFoundException(
        string message,
        string? code = null,
        IReadOnlyList<string>? path = null,
        string? requestId = null,
        Exception? innerException = null)
        : base(message, code, path, requestId, innerException)
    {
    }
}
