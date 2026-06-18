namespace Academic.Application.Abstractions;

/// <summary>
/// Port for ULID generation (ADR-036).
/// Abstracted to allow test implementations with deterministic IDs.
/// Implementation in Academic.Infrastructure (UlidGenerator wraps NUlid.Ulid.NewUlid).
/// </summary>
public interface IUlidGenerator
{
    /// <summary>Generates a new ULID string (26 characters, base32 encoded).</summary>
    /// <param name="timestamp">Optional timestamp for ordering; defaults to UtcNow.</param>
    string NewId(DateTimeOffset? timestamp = null);
}
