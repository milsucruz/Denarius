using Denarius.CrossCutting.BuildingBlocks;
using Denarius.CrossCutting.Errors;
using Denarius.CrossCutting.Results;
using Denarius.Domain.Ledger.Events;
using Denarius.Domain.Ledger.ValueObjects;

namespace Denarius.Domain.Ledger;

/// <summary>
/// Aggregate root of the Ledger bounded context.
/// Governs all state transitions for a ledger entry: recording credits and debits, and closing the period.
/// Balance is never stored — it is always projected via SUM of LedgerLine amounts at query time.
/// </summary>
public sealed class LedgerEntry : AggregateRoot<LedgerEntryId>
{
    private readonly List<LedgerLine> _lines = [];

    public AccountId AccountId { get; private set; }

    public Money InitialBalance { get; private set; }

    public LedgerStatus Status { get; private set; }

    public DateTimeOffset OpenedOn { get; private set; }

    public DateTimeOffset? ClosedOn { get; private set; }

    internal IReadOnlyList<LedgerLine> Lines => _lines.AsReadOnly();

    private LedgerEntry(
        LedgerEntryId id,
        AccountId accountId,
        Money initialBalance,
        DateTimeOffset openedOn) : base(id)
    {
        AccountId = accountId;
        InitialBalance = initialBalance;
        Status = LedgerStatus.Open;
        OpenedOn = openedOn;
    }

    // ── Factory ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a new ledger entry for the given account.
    /// Fails with <see cref="DomainError.NegativeBalance"/> when the initial balance is negative.
    /// </summary>
    public static Result<LedgerEntry> Open(
        AccountId accountId,
        Money initialBalance,
        DateTimeOffset openedOn)
    {
        if (initialBalance.Amount < 0)
        {
            return Result.Failure<LedgerEntry>(DomainError.NegativeBalance);
        }

        var entry = new LedgerEntry(LedgerEntryId.New(), accountId, initialBalance, openedOn);
        entry.Raise(new LedgerEntryOpened(entry.Id, accountId, initialBalance, openedOn));
        return Result.Success<LedgerEntry>(entry);
    }

    // ── Behaviour ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Records a credit line on this entry.
    /// Throws <see cref="DomainException"/> when the entry is closed or the amount is not positive.
    /// </summary>
    public void RecordCredit(Money amount, Description description, DateTimeOffset occurredOn)
    {
        // Invariant: a closed ledger period accepts no further mutations.
        if (Status == LedgerStatus.Closed)
        {
            throw new DomainException(DomainError.LedgerClosed);
        }

        if (amount.Amount <= 0)
        {
            throw new DomainException(DomainError.AmountMustBePositive);
        }

        _lines.Add(LedgerLine.Credit(amount));
        Raise(new CreditRecorded(Id, amount, occurredOn));
    }

    /// <summary>
    /// Applies a debit line on this entry.
    /// Throws <see cref="DomainException"/> when the entry is closed or the amount is not positive.
    /// </summary>
    public void ApplyDebit(Money amount, Description description, DateTimeOffset occurredOn)
    {
        // Invariant: a closed ledger period accepts no further mutations.
        if (Status == LedgerStatus.Closed)
        {
            throw new DomainException(DomainError.LedgerClosed);
        }

        if (amount.Amount <= 0)
        {
            throw new DomainException(DomainError.AmountMustBePositive);
        }

        _lines.Add(LedgerLine.Debit(amount));
        Raise(new DebitApplied(Id, amount, occurredOn));
    }

    /// <summary>
    /// Closes this ledger entry, preventing any further mutations.
    /// Throws <see cref="DomainException"/> when the entry is already closed.
    /// </summary>
    public void Close(DateTimeOffset closedOn)
    {
        // Invariant: closing an already-closed entry is a programming error.
        if (Status == LedgerStatus.Closed)
        {
            throw new DomainException(DomainError.LedgerClosed);
        }

        Status = LedgerStatus.Closed;
        ClosedOn = closedOn;
        Raise(new LedgerEntryClosed(Id, closedOn));
    }
}
