using FluentValidation;

namespace Attendance.Application.Attendance.RecordAttendance;

/// <summary>
/// Sync, DB-free validator for RecordAttendanceCommand (REQ-AT1-21).
/// StudentId existence check is in the HANDLER, not here.
/// </summary>
public sealed class RecordAttendanceValidator : AbstractValidator<RecordAttendanceCommand>
{
    public RecordAttendanceValidator()
    {
        RuleFor(c => c.StudentId)
            .NotEmpty().WithMessage("StudentId is required.")
            .Length(26).WithMessage("StudentId must be exactly 26 characters.");

        RuleFor(c => c.Date)
            .NotEmpty().WithMessage("Date is required.");

        RuleFor(c => c.Status)
            .NotEmpty().WithMessage("Status is required.");
    }
}
