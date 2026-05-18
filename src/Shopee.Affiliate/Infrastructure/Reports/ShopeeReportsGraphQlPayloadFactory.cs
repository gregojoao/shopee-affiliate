using Shopee.Affiliate.Reports;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Shopee.Affiliate.Infrastructure.Reports;

/// <summary>
/// Builds GraphQL request bodies for the reporting surface.
/// </summary>
/// <remarks>
/// Shopee's <c>conversionReport</c> rejects custom scalars (<c>Int64</c>) when
/// passed via <c>variables</c> — it only accepts them as literal argument
/// values. The factory therefore inlines numeric and enum arguments while
/// JSON-escaping every string argument (<c>scrollId</c>, <c>orderId</c>) to
/// prevent GraphQL injection.
/// <para>
/// Schema confirmed by live introspection against
/// <c>open-api.affiliate.shopee.com.br</c>: <c>purchaseTimeStart/End</c> are
/// <c>Int64</c>, <c>orderStatus</c> is <c>DisplayOrderStatus</c> (enum
/// <c>ALL|UNPAID|PENDING|COMPLETED|CANCELLED</c>), monetary fields come as
/// <c>String</c>, Sub Ids are encoded in <c>utmContent</c>.
/// </para>
/// </remarks>
internal static class ShopeeReportsGraphQlPayloadFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private const string ConversionReportSelection =
        " { " +
            "nodes { " +
                "conversionId conversionStatus purchaseTime clickTime " +
                "totalCommission netCommission sellerCommission shopeeCommissionCapped " +
                "buyerType device productType utmContent referrer " +
                "orders { " +
                    "orderId orderStatus " +
                    "items { " +
                        "itemId itemName imageUrl shopId shopName " +
                        "itemPrice qty actualAmount refundAmount completeTime " +
                        "itemTotalCommission itemSellerCommission itemSellerCommissionRate " +
                        "attributionType displayItemStatus " +
                    "} " +
                "} " +
            "} " +
            "pageInfo { page limit hasNextPage scrollId } " +
        "}";

    public static string BuildListConversionsPayload(
        long purchaseTimeStart,
        long purchaseTimeEnd,
        string orderStatus,
        string? subId,
        string? scrollId,
        int limit)
    {
        // NOTE: Shopee's conversionReport does not accept a subId argument.
        // Sub Id filtering is applied client-side in ShopeeAffiliateReportsClient.
        _ = subId;

        var args = new StringBuilder();
        AppendLong(args, "purchaseTimeStart", purchaseTimeStart);
        AppendLong(args, "purchaseTimeEnd", purchaseTimeEnd);
        AppendEnum(args, "orderStatus", orderStatus);
        AppendInt(args, "limit", limit);
        AppendString(args, "scrollId", scrollId);

        var query = "{ conversionReport(" + args + ")" + ConversionReportSelection + " }";
        return JsonSerializer.Serialize(new { query }, JsonOptions);
    }

    public static string BuildGetConversionPayload(string orderId, int limit = 1)
    {
        var args = new StringBuilder();
        AppendString(args, "orderId", orderId);
        AppendEnum(args, "orderStatus", "ALL");
        AppendInt(args, "limit", limit);

        var query = "{ conversionReport(" + args + ")" + ConversionReportSelection + " }";
        return JsonSerializer.Serialize(new { query }, JsonOptions);
    }

    public static string MapStatusFilter(ShopeeConversionStatusFilter? filter) => filter switch
    {
        null or ShopeeConversionStatusFilter.All => "ALL",
        ShopeeConversionStatusFilter.Pending or ShopeeConversionStatusFilter.Paid
            or ShopeeConversionStatusFilter.Shipped => "PENDING",
        ShopeeConversionStatusFilter.Completed => "COMPLETED",
        ShopeeConversionStatusFilter.Cancelled or ShopeeConversionStatusFilter.Invalid => "CANCELLED",
        _ => "ALL"
    };

    private static void AppendLong(StringBuilder args, string name, long value)
    {
        AppendSeparator(args);
        args.Append(name).Append(": ").Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendInt(StringBuilder args, string name, int value)
    {
        AppendSeparator(args);
        args.Append(name).Append(": ").Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendEnum(StringBuilder args, string name, string? enumValue)
    {
        if (string.IsNullOrWhiteSpace(enumValue)) return;
        // GraphQL enum identifiers must match [_A-Za-z][_0-9A-Za-z]* — reject anything else.
        foreach (var ch in enumValue)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '_'))
            {
                throw new ArgumentException($"Invalid GraphQL enum value '{enumValue}' for '{name}'.", nameof(enumValue));
            }
        }
        AppendSeparator(args);
        args.Append(name).Append(": ").Append(enumValue);
    }

    private static void AppendString(StringBuilder args, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        AppendSeparator(args);
        args.Append(name).Append(": ").Append(JsonSerializer.Serialize(value, JsonOptions));
    }

    private static void AppendSeparator(StringBuilder args)
    {
        if (args.Length > 0) args.Append(", ");
    }
}
