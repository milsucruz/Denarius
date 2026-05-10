namespace Denarius.Domain.Ledger.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a LedgerLine entity.
/// </summary>
public sealed class LedgerLineId : IEquatable<LedgerLineId>
{
    public Guid Value { get; }

    private LedgerLineId(Guid value)
    {
        Value = value;
    }

    public static LedgerLineId New() => new(Guid.NewGuid());

    public static LedgerLineId From(Guid value) => new(value);

    public bool Equals(LedgerLineId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as LedgerLineId);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();
}
