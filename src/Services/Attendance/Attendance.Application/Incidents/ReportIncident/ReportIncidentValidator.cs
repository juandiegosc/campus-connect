using FluentValidation;

namespace Attendance.Application.Incidents.ReportIncident;

/// <summary>
/// Sync, DB-free validator for ReportIncidentCommand (REQ-AT1-21).
/// StudentId existence check is in the HANDLER, not here.
/// </summary>
public sealed class ReportIncidentValidator : AbstractValidator<ReportIncidentCommand>
{
    public ReportIncidentValidator()
    {
        RuleFor(c => c.StudentId)
            .NotEmpty().WithMessage("StudentId is required.")
            .Length(26).WithMessage("StudentId must be exactly 26 characters.");

        RuleFor(c => c.Type)
            .NotEmpty().WithMessage("Incident type is required.");

        RuleFor(c => c.Severity)
            .NotEmpty().WithMessage("Severity is required.");

        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("Description is required.");
    }
}
