using System.Text.Json;

namespace Shopee.Affiliate.Infrastructure.Reports;

/// <summary>
/// Translates the GraphQL <c>errors[]</c> array returned by the Shopee
/// Affiliate Open API into the SDK exception hierarchy.
/// </summary>
internal static class ShopeeReportsErrorMapper
{
    public static void ThrowIfError(JsonElement responseBody, string? requestId)
    {
        if (!responseBody.TryGetProperty("errors", out var errors) ||
            errors.ValueKind != JsonValueKind.Array ||
            errors.GetArrayLength() == 0)
        {
            return;
        }

        var first = errors[0];
        var message = ReadString(first, "message");
        var code = ReadCode(first);
        var path = ReadPath(first);

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Shopee API returned GraphQL errors.";
        }

        throw CreateException(code, message, path, requestId);
    }

    private static ShopeeAffiliateException CreateException(
        string? code,
        string message,
        IReadOnlyList<string>? path,
        string? requestId)
    {
        return code switch
        {
            "10020" or "10031" or "10032" or "10033" or "10034" or "10035"
                or "INVALID_SIGNATURE" or "AUTH" or "UNAUTHORIZED"
                => new ShopeeAffiliateAuthException(message, code, path, requestId),
            "10030" or "RATE_LIMIT" or "TOO_MANY_REQUESTS"
                => new ShopeeAffiliateRateLimitException(message, code, path, requestId),
            "NOT_FOUND"
                => new ShopeeAffiliateNotFoundException(message, code, path, requestId),
            _ => new ShopeeAffiliateApiException(message, code, path, requestId)
        };
    }

    private static string? ReadCode(JsonElement error)
    {
        if (error.TryGetProperty("extensions", out var extensions) &&
            extensions.ValueKind == JsonValueKind.Object)
        {
            if (extensions.TryGetProperty("code", out var code))
            {
                return code.ValueKind switch
                {
                    JsonValueKind.String => code.GetString(),
                    JsonValueKind.Number => code.GetRawText(),
                    _ => null
                };
            }
        }

        if (error.TryGetProperty("code", out var topCode))
        {
            return topCode.ValueKind switch
            {
                JsonValueKind.String => topCode.GetString(),
                JsonValueKind.Number => topCode.GetRawText(),
                _ => null
            };
        }

        return null;
    }

    private static IReadOnlyList<string>? ReadPath(JsonElement error)
    {
        if (!error.TryGetProperty("path", out var path) ||
            path.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<string>(path.GetArrayLength());
        foreach (var segment in path.EnumerateArray())
        {
            result.Add(segment.ValueKind switch
            {
                JsonValueKind.String => segment.GetString() ?? string.Empty,
                JsonValueKind.Number => segment.GetRawText(),
                _ => string.Empty
            });
        }

        return result;
    }

    private static string ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }
}
