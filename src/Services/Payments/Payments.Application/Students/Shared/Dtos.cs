namespace Payments.Application.Students.Shared;

/// <summary>
/// Item DTO returned per student replica in paginated list.
/// Mirror of Academic's StudentListItemDto shape (ADR-059).
/// LastUpdatedAt is the consumer-clock timestamp on last upsert (ADR-059 R8).
/// Phase 3 (ADR-061): nullable AcademicStatus + FinancialStatus (Academic enum names verbatim).
/// Defaulted to null so pre-Phase-3 construction sites stay source-compatible (additive, non-breaking).
/// </summary>
public sealed record StudentReplicaItemDto(
    string   StudentId,
    string   FullName,
    string   Grade,
    string   SchoolId,
    DateTime LastUpdatedAt,
    string?  AcademicStatus  = null,
    string?  FinancialStatus = null);

/// <summary>
/// Paged list wrapper — mirrors Academic.Application.Students.Shared.PagedList&lt;T&gt; exactly.
/// Used as the single-wrap response for GetStudentsQuery (Gotcha 25 / ADR-R7).
/// </summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int Total);
