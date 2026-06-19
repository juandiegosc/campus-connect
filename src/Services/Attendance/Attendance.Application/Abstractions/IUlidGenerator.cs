namespace Attendance.Application.Abstractions;

/// <summary>
/// Port for ULID generation. Stateless; registered as singleton.
/// </summary>
public interface IUlidGenerator
{
    string NewId(DateTimeOffset? timestamp = null);
}
