namespace Denarius.Domain.Ledger.ValueObjects;

/// <summary>
/// Strongly-typed identifier for an Account in the Ledger bounded context.
/// </summary>
public sealed class AccountId : IEquatable<AccountId>
{
    public Guid Value { get; }

    private AccountId(Guid value)
    {
        Value = value;
    }

    public static AccountId New() => new(Guid.NewGuid());

    public static AccountId From(Guid value) => new(value);

    public bool Equals(AccountId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as AccountId);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();
}
