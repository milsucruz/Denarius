namespace Denarius.CrossCutting.Errors;

/// <summary>
/// Represents a typed domain error carrying a classification code and a human-readable message.
/// Use the pre-built static instances for common errors or <see cref="Create"/> for custom ones.
/// </summary>
public sealed record DomainError
{
    // ── Pre-built static instances ─────────────────────────────────────────────

    /// <summary>Sentinel value representing the absence of an error (success path).</summary>
    public static readonly DomainError None = new(DomainErrorCode.Unexpected, string.Empty);

    /// <summary>A null value was provided where a non-null value was expected.</summary>
    public static readonly DomainError NullValue = new(DomainErrorCode.Unexpected, "A null value was provided.");

    /// <summary>The requested ledger entry does not exist.</summary>
    public static readonly DomainError EntryNotFound = new(DomainErrorCode.NotFound, "The ledger entry was not found.");

    /// <summary>The initial balance of a ledger entry cannot be negative.</summary>
    public static readonly DomainError NegativeBalance = new(DomainErrorCode.Validation, "The initial balance cannot be negative.");

    /// <summary>Any monetary amount submitted to a domain operation must be greater than zero.</summary>
    public static readonly DomainError AmountMustBePositive = new(DomainErrorCode.Validation, "The amount must be greater than zero.");

    /// <summary>Arithmetic between amounts denominated in different currencies is not allowed.</summary>
    public static readonly DomainError CurrencyMismatch = new(DomainErrorCode.Conflict, "Cannot perform arithmetic on amounts with different currencies.");

    /// <summary>A closed ledger period cannot receive further mutations.</summary>
    public static readonly DomainError LedgerClosed = new(DomainErrorCode.Conflict, "The ledger period is closed and accepts no further mutations.");

    /// <summary>The amount value fails basic domain validation (e.g. format or range).</summary>
    public static readonly DomainError InvalidAmount = new(DomainErrorCode.Validation, "The provided amount is invalid.");

    // ── Properties ─────────────────────────────────────────────────────────────

    public DomainErrorCode Code { get; }

    public string Message { get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    private DomainError(DomainErrorCode code, string message)
    {
        Code = code;
        Message = message;
    }

    // ── Factory ────────────────────────────────────────────────────────────────

    /// <summary>Creates a custom domain error with the specified code and message.</summary>
    public static DomainError Create(DomainErrorCode code, string message)
    {
        return new DomainError(code, message);
    }
}
