namespace Shopee.Affiliate;

public sealed class ShopeeAffiliateApiException : Exception
{
    public ShopeeAffiliateApiException(string message)
        : base(message)
    {
    }

    public ShopeeAffiliateApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
