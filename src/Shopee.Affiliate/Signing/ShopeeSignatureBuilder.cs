using System.Security.Cryptography;
using System.Text;

namespace Shopee.Affiliate.Signing;

/// <summary>
/// Centralized builder for the SHA256 signature scheme used by every Shopee
/// Affiliate Open API request. Kept <c>internal</c> on purpose: callers should
/// go through the public clients in <c>Shopee.Affiliate.Application</c> or
/// <c>Shopee.Affiliate.Reports</c>.
/// </summary>
/// <remarks>
/// Signature contract used by Shopee: <c>SHA256(AppId + Timestamp + Payload + Secret)</c>
/// where <c>Timestamp</c> is Unix time in seconds and <c>Payload</c> is the exact JSON
/// body that will be POSTed to the GraphQL endpoint.
/// </remarks>
internal static class ShopeeSignatureBuilder
{
    public const string AuthorizationScheme = "SHA256";

    public static string CreateSignature(
        string appId,
        long timestamp,
        string payload,
        string secret)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(secret);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{appId}{timestamp}{payload}{secret}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string BuildAuthorizationHeader(
        string appId,
        long timestamp,
        string payload,
        string secret)
    {
        var signature = CreateSignature(appId, timestamp, payload, secret);
        return $"{AuthorizationScheme} Credential={appId}, Timestamp={timestamp}, Signature={signature}";
    }
}
