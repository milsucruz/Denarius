using Denarius.CrossCutting.Errors;
using Denarius.CrossCutting.Results;

namespace Denarius.Domain.Ledger.ValueObjects;

/// <summary>
/// Represents a monetary amount with an associated currency.
/// Immutable value object — equality is structural.
/// </summary>
public sealed record Money
{
    public decimal Amount { get; init; }

    public Currency Currency { get; init; }

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Creates a Money value object. Fails with <see cref="DomainError.InvalidAmount"/> if
    /// <paramref name="amount"/> is negative. Zero is valid (e.g. opening a ledger with no balance).
    /// </summary>
    public static Result<Money> Create(decimal amount, Currency currency)
    {
        if (amount < 0)
        {
            return Result.Failure<Money>(DomainError.InvalidAmount);
        }

        return Result.Success<Money>(new Money(amount, currency));
    }

    /// <summary>
    /// Adds another monetary amount to this one.
    /// Throws <see cref="DomainException"/> when the currencies differ.
    /// </summary>
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new DomainException(DomainError.CurrencyMismatch);
        }

        return this with { Amount = Amount + other.Amount };
    }

    /// <summary>Returns a new Money with the sign of Amount reversed.</summary>
    public Money Negate() => this with { Amount = -Amount };
}
