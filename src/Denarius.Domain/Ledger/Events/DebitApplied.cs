using Denarius.CrossCutting.Events;
using Denarius.Domain.Ledger.ValueObjects;

namespace Denarius.Domain.Ledger.Events;

/// <summary>
/// Raised when a debit line is applied to a LedgerEntry.
/// </summary>
public sealed record DebitApplied(
    LedgerEntryId EntryId,
    Money Amount,
    DateTimeOffset OccurredOn) : IDomainEvent;
