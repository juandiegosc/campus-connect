using Attendance.Application.Abstractions;
using NUlid;

namespace Attendance.Infrastructure.Services;

/// <summary>
/// ULID generator backed by NUlid 1.7.3. Registered as singleton — stateless.
/// Gotcha 1: API is Ulid.NewUlid(DateTimeOffset) — NOT Ulid.NewUlid() alone.
/// </summary>
internal sealed class UlidGenerator : IUlidGenerator
{
    public string NewId(DateTimeOffset? timestamp = null)
        => Ulid.NewUlid(timestamp ?? DateTimeOffset.UtcNow).ToString();
}
