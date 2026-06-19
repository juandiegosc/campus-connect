using BuildingBlocks.Contracts.Events;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Payments.Infrastructure.Messaging.Consumers;
using Payments.Infrastructure.Persistence;
using Payments.Infrastructure.Persistence.ReadModels;
using Xunit;

namespace Payments.Tests.Integration;

/// <summary>
/// Integration tests for StudentStatusUpdatedConsumer (Phase 3).
/// ESC-PM-51..54, REQ-PM3-01..04.
///
/// Assertions use a STATUS-AWARE raw SQL helper on student_replicas (Gotcha 30 — NOT harness.Published).
/// Consumer completion awaited via IConsumerTestHarness&lt;T&gt;.Consumed.Any&lt;T&gt;().
/// Replica rows are seeded via DIRECT EF insert (deterministic) before publishing the status event.
/// </summary>
[Collection("PaymentsPostgres")]
public sealed class StudentStatusUpdatedConsumerTests(PaymentsWebApplicationFactory factory)
    : IClassFixture<PaymentsWebApplicationFactory>
{
    private ITestHarness Harness => factory.Services.GetRequiredService<ITestHarness>();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NewStudentId() => NUlid.Ulid.NewUlid().ToString();

    private static StudentStatusUpdated MakeEvent(
        string studentId,
        string academicStatus  = "Active",
        string financialStatus = "Paid",
        string? correlationId   = "corr-301")
        => new()
        {
            StudentId       = studentId,
            AcademicStatus  = academicStatus,
            FinancialStatus = financialStatus,
            CorrelationId   = correlationId ?? string.Empty
        };

    private async Task SeedReplica(string studentId)
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        ctx.StudentReplicas.Add(new StudentReplica
        {
            StudentId     = studentId,
            FullName      = "Seed Student",
            Grade         = "5A",
            SchoolId      = "SCH-001",
            LastUpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await ctx.SaveChangesAsync();
    }

    private async Task<(string? AcademicStatus, string? FinancialStatus)> QueryStatus(string studentId)
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var rows = await ctx.Database.SqlQueryRaw<StatusRow>(
            "SELECT academic_status AS \"AcademicStatus\", financial_status AS \"FinancialStatus\" FROM student_replicas WHERE student_id = {0}",
            studentId).ToListAsync();
        return rows.Count == 0 ? (null, null) : (rows[0].AcademicStatus, rows[0].FinancialStatus);
    }

    private async Task<int> CountRows(string studentId)
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        return await ctx.Database.SqlQueryRaw<CountRow>(
            "SELECT COUNT(*)::int AS \"Value\" FROM student_replicas WHERE student_id = {0}",
            studentId).Select(r => r.Value).FirstAsync();
    }

    // ── ESC-PM-51: existing replica → status columns updated ──────────────────

    [Fact]
    public async Task ConsumesStatusUpdated_ExistingReplica_UpdatesStatusColumns()
    {
        var sid = NewStudentId();
        await SeedReplica(sid);

        await Harness.Bus.Publish(MakeEvent(sid, "Suspended", "Overdue"));
        await Harness.GetConsumerHarness<StudentStatusUpdatedConsumer>()
            .Consumed.Any<StudentStatusUpdated>(m => m.Context.Message.StudentId == sid);
        await Task.Delay(800);   // allow EF inbox transaction to commit

        var (academic, financial) = await QueryStatus(sid);
        academic.Should().Be("Suspended",  "ESC-PM-51: academic_status must be updated");
        financial.Should().Be("Overdue",   "ESC-PM-51: financial_status must be updated");
    }

    // ── ESC-PM-52: missing replica → no-op, no fault, no ghost row ────────────

    [Fact]
    public async Task ConsumesStatusUpdated_MissingReplica_NoOpNoFault()
    {
        var sid = NewStudentId();   // NOT seeded

        await Harness.Bus.Publish(MakeEvent(sid));
        await Harness.GetConsumerHarness<StudentStatusUpdatedConsumer>()
            .Consumed.Any<StudentStatusUpdated>(m => m.Context.Message.StudentId == sid);
        await Task.Delay(500);

        var faulted = await Harness.GetConsumerHarness<StudentStatusUpdatedConsumer>()
            .Consumed.Any<Fault<StudentStatusUpdated>>();
        faulted.Should().BeFalse("ESC-PM-52: missing replica must NOT fault (ADR-060)");

        var count = await CountRows(sid);
        count.Should().Be(0, "ESC-PM-52: no ghost row may be created");
    }

    // ── ESC-PM-53: same MessageId redelivery → status applied once, consistent ─

    [Fact]
    public async Task ConsumesStatusUpdated_SameMessageRedelivery_AppliesConsistently()
    {
        var sid = NewStudentId();
        await SeedReplica(sid);
        var evt = MakeEvent(sid, "Graduated", "Paid");

        await Harness.Bus.Publish(evt);
        await Harness.GetConsumerHarness<StudentStatusUpdatedConsumer>()
            .Consumed.Any<StudentStatusUpdated>(m => m.Context.Message.StudentId == sid);

        await Harness.Bus.Publish(evt);   // redelivery — InboxState dedup
        await Task.Delay(800);

        var count = await CountRows(sid);
        count.Should().Be(1, "ESC-PM-53: redelivery must not duplicate the row");
        var (academic, financial) = await QueryStatus(sid);
        academic.Should().Be("Graduated", "ESC-PM-53: status applied consistently");
        financial.Should().Be("Paid");
    }

    // ── ESC-PM-54: empty CorrelationId → fallback, no fault, status updated ────

    [Fact]
    public async Task ConsumesStatusUpdated_EmptyCorrelationId_DoesNotThrow()
    {
        var sid = NewStudentId();
        await SeedReplica(sid);

        await Harness.Bus.Publish(MakeEvent(sid, "Active", "Pending", correlationId: string.Empty));
        await Harness.GetConsumerHarness<StudentStatusUpdatedConsumer>()
            .Consumed.Any<StudentStatusUpdated>(m => m.Context.Message.StudentId == sid);
        await Task.Delay(800);

        var faulted = await Harness.GetConsumerHarness<StudentStatusUpdatedConsumer>()
            .Consumed.Any<Fault<StudentStatusUpdated>>();
        faulted.Should().BeFalse("ESC-PM-54: empty CorrelationId must not fault (ADR-043)");

        var (academic, _) = await QueryStatus(sid);
        academic.Should().Be("Active", "ESC-PM-54: status updated despite empty CorrelationId");
    }

    private record StatusRow(string? AcademicStatus, string? FinancialStatus);
    private record CountRow(int Value);
}
