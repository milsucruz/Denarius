using Denarius.CrossCutting.Events;
using Denarius.Domain.Ledger.ValueObjects;

namespace Denarius.Domain.Ledger.Events;

/// <summary>
/// Raised when a LedgerEntry is closed and accepts no further mutations.
/// </summary>
public sealed record LedgerEntryClosed(
    LedgerEntryId EntryId,
    DateTimeOffset ClosedOn) : IDomainEvent;
