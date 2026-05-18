namespace Shopee.Affiliate.Reports;

/// <summary>
/// Currency-aware monetary value. The SDK never converts between currencies.
/// </summary>
/// <remarks>
/// The Shopee Affiliate Open API does not return an explicit currency code on
/// <c>conversionReport</c> rows; the SDK defaults <see cref="Currency"/> to
/// the regional code of the configured endpoint (<c>BRL</c> for
/// <c>open-api.affiliate.shopee.com.br</c>). When other Shopee regions add a
/// currency field, the mapper will preserve it verbatim.
/// </remarks>
public readonly record struct Money(decimal Amount, string Currency = "BRL")
{
    public static Money Zero(string currency = "BRL") => new(0m, currency);

    public Money Add(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot add Money values with different currencies ('{Currency}' vs '{other.Currency}').");
        }

        return new Money(Amount + other.Amount, Currency);
    }
}
