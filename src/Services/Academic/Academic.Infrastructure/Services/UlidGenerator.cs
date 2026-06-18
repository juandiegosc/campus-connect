using Academic.Application.Abstractions;
using NUlid;

namespace Academic.Infrastructure.Services;

/// <summary>
/// NUlid-based implementation of <see cref="IUlidGenerator"/> (ADR-036).
/// Registered as singleton — NUlid is thread-safe.
/// </summary>
internal sealed class UlidGenerator : IUlidGenerator
{
    public string NewId(DateTimeOffset? timestamp = null)
        => timestamp.HasValue
            ? Ulid.NewUlid(timestamp.Value).ToString()
            : Ulid.NewUlid().ToString();
}
