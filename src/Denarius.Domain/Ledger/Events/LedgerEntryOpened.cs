using Denarius.CrossCutting.Events;
using Denarius.Domain.Ledger.ValueObjects;

namespace Denarius.Domain.Ledger.Events;

/// <summary>
/// Raised when a new LedgerEntry is successfully opened.
/// </summary>
public sealed record LedgerEntryOpened(
    LedgerEntryId EntryId,
    AccountId AccountId,
    Money InitialBalance,
    DateTimeOffset OpenedOn) : IDomainEvent;
