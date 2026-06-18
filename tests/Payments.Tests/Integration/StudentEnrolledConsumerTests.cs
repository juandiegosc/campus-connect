using BuildingBlocks.Contracts.Events;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Payments.Infrastructure.Messaging.Consumers;
using Payments.Infrastructure.Persistence;
using Xunit;

namespace Payments.Tests.Integration;

/// <summary>
/// Integration tests for StudentEnrolledConsumer.
/// ESC-PM-31..34, REQ-PM2-01, REQ-PM2-12.
///
/// Assertions use RAW SQL on student_replicas (Gotcha 30 / #185.3 — ADR-R6).
/// NEVER harness.Published — that checks outbound outbox messages, not read-model writes.
/// Consumer completion awaited via IConsumerTestHarness&lt;T&gt;.Consumed.Any&lt;T&gt;() (Gotcha #185.2).
/// </summary>
[Collection("PaymentsPostgres")]
public sealed class StudentEnrolledConsumerTests(PaymentsWebApplicationFactory factory)
    : IClassFixture<PaymentsWebApplicationFactory>
{
    private ITestHarness Harness => factory.Services.GetRequiredService<ITestHarness>();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StudentEnrolled MakeEvent(
        string? studentId     = null,
        string? fullName      = null,
        string? grade         = null,
        string? correlationId = "corr-001")
    {
        var sid = studentId ?? NUlid.Ulid.NewUlid().ToString();
        return new StudentEnrolled
        {
            StudentId     = sid,
            EnrollmentId  = NUlid.Ulid.NewUlid().ToString(),
            SchoolId      = "SCH-001",
            Grade         = grade    ?? "5A",
            FullName      = fullName ?? "Ana Torres",
            CorrelationId = correlationId ?? string.Empty   // IntegrationEvent.CorrelationId is non-null string
        };
    }

    private async Task<(string? StudentId, string? FullName, string? Grade, string? SchoolId)>
        QueryReplicaRow(string studentId)
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

        // Raw SQL assertion — ADR-R6, REQ-PM2-12
        var rows = await ctx.Database.SqlQueryRaw<ReplicaRow>(
            "SELECT student_id AS \"StudentId\", full_name AS \"FullName\", grade AS \"Grade\", school_id AS \"SchoolId\" FROM student_replicas WHERE student_id = {0}",
            studentId).ToListAsync();

        if (rows.Count == 0) return (null, null, null, null);
        return (rows[0].StudentId, rows[0].FullName, rows[0].Grade, rows[0].SchoolId);
    }

    private async Task<int> CountReplicaRows(string studentId)
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        return await ctx.Database.SqlQueryRaw<CountRow>(
            "SELECT COUNT(*)::int AS \"Value\" FROM student_replicas WHERE student_id = {0}",
            studentId).Select(r => r.Value).FirstAsync();
    }

    // ── ESC-PM-31: First-time StudentEnrolled → replica row created ───────────

    [Fact]
    public async Task ConsumesStudentEnrolled_PersistsReplicaRow_ViaSql()
    {
        var evt = MakeEvent(fullName: "Ana Torres", grade: "5A");

        await Harness.Bus.Publish(evt);
        await Harness.GetConsumerHarness<StudentEnrolledConsumer>()
            .Consumed.Any<StudentEnrolled>();

        var (sid, fullName, grade, schoolId) = await QueryReplicaRow(evt.StudentId);

        sid.Should().Be(evt.StudentId,     "ESC-PM-31: replica row must exist");
        fullName.Should().Be("Ana Torres", "FullName must match event");
        grade.Should().Be("5A",            "Grade must match event");
        schoolId.Should().Be("SCH-001",    "SchoolId must match event");
    }

    // ── ESC-PM-32: Same MessageId redelivery → exactly one row (InboxState dedup) ─

    [Fact]
    public async Task ConsumesStudentEnrolled_SameMessageId_NoDuplicateRow()
    {
        var sid = NUlid.Ulid.NewUlid().ToString();
        var evt = MakeEvent(studentId: sid, fullName: "Bob Smith", grade: "6B");

        // Publish same event twice
        await Harness.Bus.Publish(evt);
        await Harness.GetConsumerHarness<StudentEnrolledConsumer>()
            .Consumed.Any<StudentEnrolled>();

        // Publish again — InboxState dedup should prevent double-processing
        await Harness.Bus.Publish(evt);
        await Task.Delay(500);   // give harness time to route the duplicate

        var count = await CountReplicaRows(sid);
        count.Should().Be(1, "ESC-PM-32/50: InboxState dedup must prevent duplicate rows");
    }

    // ── ESC-PM-33: Different MessageId, same StudentId → upsert updates row ───

    [Fact]
    public async Task ConsumesStudentEnrolled_DifferentMessageId_SameStudentId_UpdatesRow()
    {
        var sid  = NUlid.Ulid.NewUlid().ToString();
        var evt1 = MakeEvent(studentId: sid, fullName: "Original Name", grade: "5A");
        var evt2 = MakeEvent(studentId: sid, fullName: "Updated Name",  grade: "6B");

        // Publish first event and wait for THIS specific message to be consumed + DB commit
        await Harness.Bus.Publish(evt1);
        await Harness.GetConsumerHarness<StudentEnrolledConsumer>()
            .Consumed.Any<StudentEnrolled>(m => m.Context.Message.StudentId == sid && m.Context.Message.FullName == "Original Name");
        await Task.Delay(800);   // allow MassTransit EF inbox transaction to commit to DB

        // Publish second event (different MessageId, same StudentId) and wait
        await Harness.Bus.Publish(evt2);
        await Harness.GetConsumerHarness<StudentEnrolledConsumer>()
            .Consumed.Any<StudentEnrolled>(m => m.Context.Message.StudentId == sid && m.Context.Message.FullName == "Updated Name");
        await Task.Delay(800);   // allow second commit

        // Final assertions: one row, with updated fields (ESC-PM-33)
        var count = await CountReplicaRows(sid);
        count.Should().Be(1, "ESC-PM-33: upsert must not create duplicate row");

        var (_, fullName, grade, _) = await QueryReplicaRow(sid);
        fullName.Should().Be("Updated Name", "ESC-PM-33: upsert must update FullName");
        grade.Should().Be("6B",              "ESC-PM-33: upsert must update Grade");
    }

    // ── ESC-PM-34: Empty CorrelationId → fallback, consumer does not throw ───

    [Fact]
    public async Task ConsumesStudentEnrolled_EmptyCorrelationId_DoesNotThrow()
    {
        // IntegrationEvent.CorrelationId is a non-nullable string — use empty string to
        // trigger the IsNullOrEmpty fallback (ADR-043). The consumer logs a warning + continues.
        var evt = MakeEvent(correlationId: string.Empty);

        await Harness.Bus.Publish(evt);
        await Harness.GetConsumerHarness<StudentEnrolledConsumer>()
            .Consumed.Any<StudentEnrolled>();

        // No fault should have been raised
        var faulted = await Harness.GetConsumerHarness<StudentEnrolledConsumer>()
            .Consumed.Any<Fault<StudentEnrolled>>();
        faulted.Should().BeFalse("ESC-PM-34: consumer must not throw on empty CorrelationId");

        var count = await CountReplicaRows(evt.StudentId);
        count.Should().Be(1, "replica row must be created even with empty CorrelationId");
    }

    // ── Projection types for raw SQL ─────────────────────────────────────────

    private record ReplicaRow(string StudentId, string FullName, string Grade, string SchoolId);
    private record CountRow(int Value);
}
