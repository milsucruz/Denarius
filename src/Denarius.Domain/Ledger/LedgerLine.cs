using Denarius.CrossCutting.BuildingBlocks;
using Denarius.Domain.Ledger.ValueObjects;

namespace Denarius.Domain.Ledger;

/// <summary>
/// A single credit or debit row inside a LedgerEntry.
/// Internal to the aggregate — never persisted or queried in isolation.
/// Sign convention: credit lines carry a positive amount; debit lines carry a negative amount.
/// </summary>
internal sealed class LedgerLine : Entity<LedgerLineId>
{
    public Money Amount { get; }

    private LedgerLine(LedgerLineId id, Money amount) : base(id)
    {
        Amount = amount;
    }

    /// <summary>Creates a credit line. Amount is always stored as a positive value.</summary>
    internal static LedgerLine Credit(Money amount) =>
        new(LedgerLineId.New(), amount with { Amount = Math.Abs(amount.Amount) });

    /// <summary>Creates a debit line. Amount is always stored as a negative value.</summary>
    internal static LedgerLine Debit(Money amount) =>
        new(LedgerLineId.New(), amount with { Amount = -Math.Abs(amount.Amount) });
}
