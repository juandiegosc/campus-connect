using Analytics.Application.Abstractions;
using Analytics.Application.Dashboard;
using Analytics.Application.Events;
using Analytics.Domain.Projections;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IAnalyticsRepository.
/// Write methods run OUTSIDE the MediatR UoW pipeline (called from consumers) and therefore
/// commit their own SaveChanges. Each is idempotent: a duplicate EventId is silently ignored
/// (explicit idempotent-receiver pattern, layered on top of the MassTransit EF inbox).
/// </summary>
internal sealed class AnalyticsRepository(AnalyticsDbContext ctx) : IAnalyticsRepository
{
    public async Task<bool> RecordEventAsync(
        Guid eventId, string eventType, string? entityId, string? correlationId,
        DateTime occurredAt, CancellationToken ct = default)
    {
        if (await ctx.ProcessedEvents.AnyAsync(e => e.EventId == eventId, ct))
            return false;

        ctx.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = eventId,
            EventType = eventType,
            EntityId = entityId,
            CorrelationId = correlationId,
            OccurredAt = occurredAt,
            ReceivedAt = DateTime.UtcNow
        });

        await ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task RecordStudentEnrolledAsync(
        Guid eventId, string? correlationId, DateTime occurredAt,
        string studentId, string fullName, string grade, CancellationToken ct = default)
    {
        if (await ctx.ProcessedEvents.AnyAsync(e => e.EventId == eventId, ct))
            return;

        ctx.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = eventId,
            EventType = "StudentEnrolled",
            EntityId = studentId,
            CorrelationId = correlationId,
            OccurredAt = occurredAt,
            ReceivedAt = DateTime.UtcNow
        });

        var existing = await ctx.StudentProjections.FindAsync([studentId], ct);
        if (existing is null)
        {
            ctx.StudentProjections.Add(new StudentProjection
            {
                StudentId = studentId,
                FullName = fullName,
                Grade = grade,
                AcademicStatus = "Active",
                FinancialStatus = "Pending",
                LastUpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.FullName = fullName;
            existing.Grade = grade;
            existing.LastUpdatedAt = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync(ct);
    }

    public async Task RecordStudentStatusUpdatedAsync(
        Guid eventId, string? correlationId, DateTime occurredAt,
        string studentId, string academicStatus, string financialStatus, CancellationToken ct = default)
    {
        if (await ctx.ProcessedEvents.AnyAsync(e => e.EventId == eventId, ct))
            return;

        ctx.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = eventId,
            EventType = "StudentStatusUpdated",
            EntityId = studentId,
            CorrelationId = correlationId,
            OccurredAt = occurredAt,
            ReceivedAt = DateTime.UtcNow
        });

        var existing = await ctx.StudentProjections.FindAsync([studentId], ct);
        if (existing is null)
        {
            // Status arrived before enrollment projection — create a placeholder.
            ctx.StudentProjections.Add(new StudentProjection
            {
                StudentId = studentId,
                FullName = "(desconocido)",
                Grade = "(desconocido)",
                AcademicStatus = academicStatus,
                FinancialStatus = financialStatus,
                LastUpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.AcademicStatus = academicStatus;
            existing.FinancialStatus = financialStatus;
            existing.LastUpdatedAt = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync(ct);
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var totalStudents = await ctx.StudentProjections.CountAsync(ct);
        var paymentsPending = await ctx.StudentProjections.CountAsync(s => s.FinancialStatus != "Paid", ct);
        var paymentsConfirmed = await ctx.ProcessedEvents.CountAsync(e => e.EventType == "PaymentConfirmed", ct);
        var attendanceRecorded = await ctx.ProcessedEvents.CountAsync(e => e.EventType == "AttendanceRecorded", ct);
        var incidentsReported = await ctx.ProcessedEvents.CountAsync(e => e.EventType == "IncidentReported", ct);
        var notificationsSent = await ctx.ProcessedEvents.CountAsync(e => e.EventType == "NotificationSent", ct);
        var failedMessages = await ctx.ProcessedEvents.CountAsync(e => e.EventType == "NotificationFailed", ct);
        var eventsProcessed = await ctx.ProcessedEvents.CountAsync(ct);

        return new DashboardDto(
            TotalStudents: totalStudents,
            PaymentsConfirmed: paymentsConfirmed,
            PaymentsPending: paymentsPending,
            AttendanceRecorded: attendanceRecorded,
            IncidentsReported: incidentsReported,
            NotificationsSent: notificationsSent,
            EventsProcessed: eventsProcessed,
            FailedMessages: failedMessages,
            Status: failedMessages == 0 ? "ok" : "degraded",
            GeneratedAt: DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<EventLogDto>> GetRecentEventsAsync(int take, CancellationToken ct = default)
        => await ctx.ProcessedEvents
            .AsNoTracking()
            .OrderByDescending(e => e.ReceivedAt)
            .Take(take)
            .Select(e => new EventLogDto(
                e.EventType,
                e.EntityId,
                e.CorrelationId,
                e.OccurredAt,
                e.ReceivedAt))
            .ToListAsync(ct);
}
