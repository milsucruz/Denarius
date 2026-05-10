using Denarius.CrossCutting.Events;
using Denarius.Domain.Ledger.ValueObjects;

namespace Denarius.Domain.Ledger.Events;

/// <summary>
/// Raised when a credit line is recorded on a LedgerEntry.
/// </summary>
public sealed record CreditRecorded(
    LedgerEntryId EntryId,
    Money Amount,
    DateTimeOffset OccurredOn) : IDomainEvent;
