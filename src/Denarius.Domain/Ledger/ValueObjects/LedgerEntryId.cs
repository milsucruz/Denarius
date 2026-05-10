namespace Denarius.Domain.Ledger.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a LedgerEntry aggregate root.
/// </summary>
public sealed class LedgerEntryId : IEquatable<LedgerEntryId>
{
    public Guid Value { get; }

    private LedgerEntryId(Guid value)
    {
        Value = value;
    }

    public static LedgerEntryId New() => new(Guid.NewGuid());

    public static LedgerEntryId From(Guid value) => new(value);

    public bool Equals(LedgerEntryId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as LedgerEntryId);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();
}
