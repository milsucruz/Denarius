namespace Denarius.CrossCutting.Errors;

/// <summary>
/// Classifies a domain error so that API and application boundaries
/// can map failures to the appropriate HTTP status codes without
/// inspecting error messages.
/// </summary>
public enum DomainErrorCode
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Unexpected,
}
