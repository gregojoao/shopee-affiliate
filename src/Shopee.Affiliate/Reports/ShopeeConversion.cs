namespace Shopee.Affiliate.Reports;

/// <summary>
/// One affiliate conversion row returned by <c>conversionReport</c>.
/// </summary>
/// <param name="ConversionId">Shopee conversion id (unique per attributed click).</param>
/// <param name="OrderId">Shopee order id.</param>
/// <param name="Status">Normalized order status.</param>
/// <param name="ShopId">Seller shop id (when available).</param>
/// <param name="ShopName">Seller shop name (when available).</param>
/// <param name="ProductId">Primary item id of the order (when available).</param>
/// <param name="ProductTitle">Primary item name.</param>
/// <param name="ProductImageUrl">
/// Product image. <c>conversionReport</c> does not return image URLs; the SDK
/// surfaces <c>null</c> unless callers enrich the row via <c>productOfferV2</c>.
/// </param>
/// <param name="Quantity">Total units across the order.</param>
/// <param name="ItemPrice">Unit price (gross).</param>
/// <param name="TotalSale">Gross sale value (price × quantity).</param>
/// <param name="Commission">Net commission attributed to the affiliate.</param>
/// <param name="CommissionRate">Commission rate expressed as 0..1 (e.g. 0.07 = 7%).</param>
/// <param name="SubIds">SubId slots 1..5 set when the affiliate link was generated.</param>
/// <param name="ClickTime">Click that originated the conversion.</param>
/// <param name="PurchaseTime">Order purchase time.</param>
/// <param name="CompleteTime">Order completion time (when Shopee finalizes payout).</param>
/// <param name="Currency">
/// ISO 4217 code. The Shopee <c>conversionReport</c> schema does not currently
/// return a currency field, so the SDK defaults this to <c>BRL</c> for the
/// Brazil endpoint. Preserved verbatim once Shopee adds the field.
/// </param>
/// <param name="RawJson">Original GraphQL <c>node</c> JSON for debugging/integration.</param>
public record ShopeeConversion(
    string ConversionId,
    string OrderId,
    ShopeeOrderStatus Status,
    string? ShopId,
    string? ShopName,
    string? ProductId,
    string? ProductTitle,
    string? ProductImageUrl,
    int Quantity,
    Money ItemPrice,
    Money TotalSale,
    Money Commission,
    decimal CommissionRate,
    IReadOnlyList<string> SubIds,
    DateTimeOffset? ClickTime,
    DateTimeOffset PurchaseTime,
    DateTimeOffset? CompleteTime,
    string? Currency,
    string? RawJson);
