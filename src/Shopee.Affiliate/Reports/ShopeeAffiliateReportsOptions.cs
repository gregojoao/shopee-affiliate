namespace Shopee.Affiliate.Reports;

/// <summary>
/// Configuration for <see cref="ShopeeAffiliateReportsClient"/>. Mirrors
/// <see cref="Shopee.Affiliate.Application.ShopeeAffiliateOptions"/> but is
/// scoped to the reporting endpoints so callers can sign reporting traffic
/// with a separate AppId/Secret when needed.
/// </summary>
public sealed class ShopeeAffiliateReportsOptions
{
    public static readonly Uri DefaultEndpoint = new("https://open-api.affiliate.shopee.com.br/graphql");
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public Uri Endpoint { get; set; } = DefaultEndpoint;

    public string AppId { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public TimeSpan Timeout { get; set; } = DefaultTimeout;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AppId))
        {
            throw new InvalidOperationException("Shopee affiliate reports AppId is required.");
        }

        if (string.IsNullOrWhiteSpace(Secret))
        {
            throw new InvalidOperationException("Shopee affiliate reports Secret is required.");
        }

        if (Endpoint is null ||
            (Endpoint.Scheme != Uri.UriSchemeHttp && Endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Shopee affiliate reports Endpoint must be an absolute HTTP/HTTPS URL.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Shopee affiliate reports Timeout must be greater than zero.");
        }
    }
}
