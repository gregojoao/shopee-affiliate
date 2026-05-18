namespace Shopee.Affiliate.Infrastructure;

/// <summary>
/// Base class for all Shopee Affiliate SDK exceptions. Catch this to handle any
/// failure originating from the SDK (link generation, product offer lookup, or
/// the reporting client).
/// </summary>
public abstract class ShopeeAffiliateException : Exception
{
    /// <summary>Optional Shopee error code (e.g. <c>10020</c>, <c>10030</c>).</summary>
    public string? Code { get; }

    /// <summary>Optional GraphQL field path returned by the server.</summary>
    public IReadOnlyList<string>? Path { get; }

    /// <summary>Optional request id returned by Shopee for correlation.</summary>
    public string? RequestId { get; }

    protected ShopeeAffiliateException(
        string message,
        string? code = null,
        IReadOnlyList<string>? path = null,
        string? requestId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Path = path;
        RequestId = requestId;
    }
}
