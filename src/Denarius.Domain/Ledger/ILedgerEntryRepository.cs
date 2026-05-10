using Denarius.Domain.Ledger.ValueObjects;

namespace Denarius.Domain.Ledger;

/// <summary>
/// Repository contract for the LedgerEntry aggregate root.
/// Implementations live in Denarius.Infrastructure.
/// </summary>
public interface ILedgerEntryRepository
{
    Task<LedgerEntry?> FindByIdAsync(LedgerEntryId id, CancellationToken ct);

    Task AddAsync(LedgerEntry entry, CancellationToken ct);
}
