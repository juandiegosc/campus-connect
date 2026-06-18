namespace Academic.Application.Students.EnrollStudent;

/// <summary>Response returned by EnrollStudentCommandHandler on success.</summary>
public sealed record EnrollStudentResponse(
    string StudentId,
    string EnrollmentId,
    string Status  // always "Active" on creation
);
