namespace Academic.Application.Students.Shared;

/// <summary>List item DTO for GET /api/academic/students paginated response.</summary>
public sealed record StudentListItemDto(
    string StudentId,
    string FullName,
    string Grade,
    string AcademicStatus,
    string FinancialStatus);

/// <summary>Detail DTO for GET /api/academic/students/{id} response.</summary>
public sealed record StudentDetailDto(
    string StudentId,
    string FullName,
    string DocumentId,
    string Grade,
    string SchoolId,
    string AcademicStatus,
    string FinancialStatus,
    GuardianDto Guardian);

/// <summary>Guardian sub-object DTO.</summary>
public sealed record GuardianDto(string Name, string Email);

/// <summary>Status DTO for GET /api/academic/students/{id}/status response.</summary>
public sealed record StudentStatusDto(
    string StudentId,
    bool   Exists,
    string AcademicStatus,
    string FinancialStatus);

/// <summary>Event DTO for GET /api/academic/students/{id}/events response.</summary>
public sealed record StudentEventDto(
    string   EventType,
    DateTime OccurredAt,
    string   CorrelationId);

/// <summary>Paged list wrapper for query responses.</summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int Total);
