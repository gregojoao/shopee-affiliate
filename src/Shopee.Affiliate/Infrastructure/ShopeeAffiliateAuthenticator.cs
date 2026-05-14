using System.Security.Cryptography;
using System.Text;

namespace Shopee.Affiliate;

internal static class ShopeeAffiliateAuthenticator
{
    public static string BuildAuthorizationHeader(
        string appId,
        long timestamp,
        string payload,
        string secret)
    {
        var signature = CreateSignature(appId, timestamp, payload, secret);
        return $"SHA256 Credential={appId}, Timestamp={timestamp}, Signature={signature}";
    }

    public static string CreateSignature(
        string appId,
        long timestamp,
        string payload,
        string secret)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{appId}{timestamp}{payload}{secret}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
