using Shopee.Affiliate.Reports;
using System.Globalization;
using System.Text.Json;

namespace Shopee.Affiliate.Infrastructure.Reports;

/// <summary>
/// Maps Shopee's <c>conversionReport</c> JSON payloads into the public DTOs.
/// </summary>
/// <remarks>
/// Field shapes used here were confirmed by GraphQL introspection against
/// <c>open-api.affiliate.shopee.com.br</c>: monetary amounts come as
/// <c>String</c>, identifiers come as <c>Int64</c>, <c>completeTime</c> lives
/// on the <c>orders[].items[]</c> record (not the conversion root), and Sub
/// Ids are encoded inside <c>utmContent</c>.
/// </remarks>
internal static class ShopeeReportsResponseMapper
{
    private const string DefaultCurrency = "BRL";
    private static readonly char[] SubIdDelimiters = { '_', '|', ',' };

    public static ShopeeConversionPage MapConversionPage(
        JsonElement responseBody,
        int requestedPage,
        int requestedLimit)
    {
        var report = NavigateConversionReport(responseBody);
        var items = new List<ShopeeConversion>();

        if (report.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in nodes.EnumerateArray())
            {
                items.Add(MapConversion(node, includeRaw: true));
            }
        }

        var pageInfo = report.TryGetProperty("pageInfo", out var pi) && pi.ValueKind == JsonValueKind.Object
            ? pi
            : default;

        var hasNextPage = pageInfo.ValueKind == JsonValueKind.Object &&
                          pageInfo.TryGetProperty("hasNextPage", out var hn) &&
                          hn.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                          hn.GetBoolean();
        var nextCursor = pageInfo.ValueKind == JsonValueKind.Object
            ? ReadOptionalString(pageInfo, "scrollId")
            : null;
        var limit = pageInfo.ValueKind == JsonValueKind.Object &&
                    pageInfo.TryGetProperty("limit", out var lim) && lim.ValueKind == JsonValueKind.Number
            ? lim.GetInt32()
            : requestedLimit;

        return new ShopeeConversionPage(
            Items: items,
            Page: requestedPage,
            PageSize: limit,
            TotalCount: null,
            NextCursor: nextCursor,
            HasMore: hasNextPage);
    }

    public static ShopeeConversionDetail? MapConversionDetail(JsonElement responseBody)
    {
        var report = NavigateConversionReport(responseBody);
        if (!report.TryGetProperty("nodes", out var nodes) ||
            nodes.ValueKind != JsonValueKind.Array ||
            nodes.GetArrayLength() == 0)
        {
            return null;
        }

        var node = nodes[0];
        var conversion = MapConversion(node, includeRaw: true);
        var lines = ExtractOrderLines(node, conversion.Currency ?? DefaultCurrency);

        return new ShopeeConversionDetail(
            ConversionId: conversion.ConversionId,
            OrderId: conversion.OrderId,
            Status: conversion.Status,
            ShopId: conversion.ShopId,
            ShopName: conversion.ShopName,
            ProductId: conversion.ProductId,
            ProductTitle: conversion.ProductTitle,
            ProductImageUrl: conversion.ProductImageUrl,
            Quantity: conversion.Quantity,
            ItemPrice: conversion.ItemPrice,
            TotalSale: conversion.TotalSale,
            Commission: conversion.Commission,
            CommissionRate: conversion.CommissionRate,
            SubIds: conversion.SubIds,
            ClickTime: conversion.ClickTime,
            PurchaseTime: conversion.PurchaseTime,
            CompleteTime: conversion.CompleteTime,
            Currency: conversion.Currency,
            RawJson: conversion.RawJson,
            Lines: lines);
    }

    public static ShopeeConversion MapConversion(JsonElement node, bool includeRaw)
    {
        var conversionId = ReadIdAsString(node, "conversionId");
        var purchaseTime = ReadUnixSeconds(node, "purchaseTime");
        var clickTime = ReadOptionalUnixSeconds(node, "clickTime");
        var totalCommissionAmount = ReadDecimal(node, "totalCommission");
        var netCommissionAmount = ReadDecimal(node, "netCommission");
        var commissionAmount = netCommissionAmount > 0 ? netCommissionAmount : totalCommissionAmount;
        var utmContent = ReadOptionalString(node, "utmContent");
        var subIds = ParseSubIds(utmContent);

        var primaryOrder = TryGetFirst(node, "orders");
        var orderId = primaryOrder.HasValue ? ReadString(primaryOrder.Value, "orderId") : string.Empty;
        var status = MapStatus(primaryOrder.HasValue ? ReadString(primaryOrder.Value, "orderStatus") : null);

        var primaryItem = primaryOrder.HasValue ? TryGetFirst(primaryOrder.Value, "items") : null;
        var productId = primaryItem.HasValue ? ReadIdAsString(primaryItem.Value, "itemId") : null;
        var productTitle = primaryItem.HasValue ? ReadOptionalString(primaryItem.Value, "itemName") : null;
        var productImageUrl = primaryItem.HasValue ? ReadOptionalString(primaryItem.Value, "imageUrl") : null;
        var shopId = primaryItem.HasValue ? ReadIdAsString(primaryItem.Value, "shopId") : null;
        var shopName = primaryItem.HasValue ? ReadOptionalString(primaryItem.Value, "shopName") : null;
        var itemPriceAmount = primaryItem.HasValue ? ReadDecimal(primaryItem.Value, "itemPrice") : 0m;
        var completeTime = primaryItem.HasValue ? ReadOptionalUnixSeconds(primaryItem.Value, "completeTime") : null;

        var quantity = SumQuantity(primaryOrder);
        var totalSale = SumTotalSale(primaryOrder);
        var commissionRate = totalSale > 0
            ? Math.Round(commissionAmount / totalSale, 4, MidpointRounding.AwayFromZero)
            : 0m;
        var currency = DefaultCurrency;

        return new ShopeeConversion(
            ConversionId: string.IsNullOrEmpty(conversionId) ? orderId : conversionId!,
            OrderId: orderId,
            Status: status,
            ShopId: shopId,
            ShopName: shopName,
            ProductId: productId,
            ProductTitle: productTitle,
            ProductImageUrl: productImageUrl,
            Quantity: quantity,
            ItemPrice: new Money(itemPriceAmount, currency),
            TotalSale: new Money(totalSale, currency),
            Commission: new Money(commissionAmount, currency),
            CommissionRate: commissionRate,
            SubIds: subIds,
            ClickTime: clickTime,
            PurchaseTime: purchaseTime,
            CompleteTime: completeTime,
            Currency: currency,
            RawJson: includeRaw ? node.GetRawText() : null);
    }

    private static IReadOnlyList<ShopeeOrderLine> ExtractOrderLines(JsonElement node, string currency)
    {
        var lines = new List<ShopeeOrderLine>();
        if (!node.TryGetProperty("orders", out var orders) || orders.ValueKind != JsonValueKind.Array)
        {
            return lines;
        }

        foreach (var order in orders.EnumerateArray())
        {
            if (!order.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in items.EnumerateArray())
            {
                lines.Add(new ShopeeOrderLine(
                    ItemId: ReadIdAsString(item, "itemId"),
                    ItemName: ReadOptionalString(item, "itemName"),
                    ShopId: ReadIdAsString(item, "shopId"),
                    ShopName: ReadOptionalString(item, "shopName"),
                    Quantity: ReadInt(item, "qty"),
                    ItemPrice: new Money(ReadDecimal(item, "itemPrice"), currency),
                    ItemCommission: new Money(ReadDecimal(item, "itemTotalCommission"), currency),
                    RefundAmount: new Money(ReadDecimal(item, "refundAmount"), currency),
                    AttributionType: ReadOptionalString(item, "attributionType")));
            }
        }

        return lines;
    }

    private static IReadOnlyList<string> ParseSubIds(string? utmContent)
    {
        if (string.IsNullOrWhiteSpace(utmContent)) return Array.Empty<string>();

        var parts = utmContent.Split(SubIdDelimiters, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(capacity: Math.Min(parts.Length, 5));
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
                if (result.Count == 5) break;
            }
        }
        return result;
    }

    private static int SumQuantity(JsonElement? order)
    {
        if (!order.HasValue ||
            !order.Value.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var total = 0;
        foreach (var item in items.EnumerateArray())
        {
            total += ReadInt(item, "qty");
        }
        return total;
    }

    private static decimal SumTotalSale(JsonElement? order)
    {
        if (!order.HasValue ||
            !order.Value.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return 0m;
        }

        var total = 0m;
        foreach (var item in items.EnumerateArray())
        {
            // Prefer actualAmount (post-discount, what the buyer actually paid).
            var actual = ReadDecimal(item, "actualAmount");
            if (actual > 0)
            {
                total += actual;
                continue;
            }
            var price = ReadDecimal(item, "itemPrice");
            var qty = ReadInt(item, "qty");
            total += price * qty;
        }
        return total;
    }

    public static ShopeeOrderStatus MapStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ShopeeOrderStatus.Unknown;
        return raw.Trim().ToUpperInvariant() switch
        {
            "UNPAID" or "PENDING" => ShopeeOrderStatus.Pending,
            "PAID" => ShopeeOrderStatus.Paid,
            "SHIPPED" or "TO_RECEIVE" or "SHIPPING" => ShopeeOrderStatus.Shipped,
            "COMPLETED" or "DONE" => ShopeeOrderStatus.Completed,
            "CANCELLED" or "CANCELED" => ShopeeOrderStatus.Cancelled,
            "INVALID" or "FRAUD" => ShopeeOrderStatus.Invalid,
            _ => ShopeeOrderStatus.Unknown
        };
    }

    private static JsonElement NavigateConversionReport(JsonElement responseBody)
    {
        if (responseBody.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("conversionReport", out var report) &&
            report.ValueKind == JsonValueKind.Object)
        {
            return report;
        }

        return default;
    }

    private static JsonElement? TryGetFirst(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array ||
            array.GetArrayLength() == 0)
        {
            return null;
        }
        return array[0];
    }

    private static string ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property)
            ? GetStringValue(property)
            : string.Empty;
    }

    private static string? ReadIdAsString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property)) return null;
        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() is { Length: > 0 } v ? v : null,
            JsonValueKind.Number => property.TryGetInt64(out var l) ? l.ToString(CultureInfo.InvariantCulture) : property.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => null
        };
    }

    private static string? ReadOptionalString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property)) return null;
        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() is { Length: > 0 } v ? v : null,
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => null
        };
    }

    private static int ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property)) return 0;
        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetInt32(out var i) ? i : (int)property.GetDouble(),
            JsonValueKind.String => int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0,
            _ => 0
        };
    }

    private static decimal ReadDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property)) return 0m;
        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetDecimal(out var d) ? d : (decimal)property.GetDouble(),
            JsonValueKind.String => decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m,
            _ => 0m
        };
    }

    private static DateTimeOffset ReadUnixSeconds(JsonElement element, string name)
    {
        var value = ReadLong(element, name);
        return DateTimeOffset.FromUnixTimeSeconds(value);
    }

    private static DateTimeOffset? ReadOptionalUnixSeconds(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property)) return null;
        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt64(out var l) && l > 0 => DateTimeOffset.FromUnixTimeSeconds(l),
            JsonValueKind.String when long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) && l > 0
                => DateTimeOffset.FromUnixTimeSeconds(l),
            _ => null
        };
    }

    private static long ReadLong(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property)) return 0L;
        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetInt64(out var l) ? l : (long)property.GetDouble(),
            JsonValueKind.String => long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : 0L,
            _ => 0L
        };
    }

    private static string GetStringValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => string.Empty
    };
}
