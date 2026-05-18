namespace Shopee.Affiliate.Reports;

/// <summary>
/// Currency-aware monetary value. The SDK never converts between currencies —
/// the <see cref="Currency"/> code returned by Shopee is preserved verbatim.
/// </summary>
/// <remarks>
/// Shopee Brazil always returns <c>BRL</c>. Other Shopee regions return the
/// local ISO 4217 code (e.g. <c>IDR</c>, <c>SGD</c>, <c>VND</c>).
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
