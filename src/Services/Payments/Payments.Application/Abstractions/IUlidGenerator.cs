namespace Payments.Application.Abstractions;

/// <summary>
/// Port for ULID generation (ADR-036 carry-over). Stateless; registered as singleton.
/// </summary>
public interface IUlidGenerator
{
    string NewId(DateTimeOffset? timestamp = null);
}
