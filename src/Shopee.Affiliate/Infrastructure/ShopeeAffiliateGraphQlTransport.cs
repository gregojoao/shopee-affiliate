using Shopee.Affiliate.Application;
using System.Text;
using System.Text.Json;

namespace Shopee.Affiliate.Infrastructure;

internal sealed class ShopeeAffiliateGraphQlTransport : IShopeeAffiliateGraphQlTransport
{
    private readonly HttpClient _httpClient;
    private readonly Func<long> _nowSeconds;

    public ShopeeAffiliateGraphQlTransport(HttpClient httpClient, Func<long> nowSeconds)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _nowSeconds = nowSeconds ?? throw new ArgumentNullException(nameof(nowSeconds));
    }

    public async Task<ShopeeAffiliateGraphQlResponse> PostAsync(
        string payload,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken)
    {
        var timestamp = _nowSeconds();
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            ShopeeAffiliateAuthenticator.BuildAuthorizationHeader(options.AppId, timestamp, payload, options.Secret));
        request.Headers.UserAgent.ParseAdd(ShopeeAffiliateDefaults.UserAgent);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseBody = ParseResponseBody(text);

            if (!response.IsSuccessStatusCode)
            {
                using (responseBody)
                {
                    throw new ShopeeAffiliateApiException(
                        $"Shopee API HTTP {(int)response.StatusCode}: {Truncate(text, 1000)}");
                }
            }

            return new ShopeeAffiliateGraphQlResponse(responseBody, text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShopeeAffiliateApiException($"Shopee API request timed out after {options.Timeout.TotalMilliseconds:0}ms.");
        }
    }

    private static JsonDocument ParseResponseBody(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text)
                ? JsonDocument.Parse("{}")
                : JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new ShopeeAffiliateApiException(
                $"Shopee API returned non-JSON response: {Truncate(text, 500)}",
                ex);
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
