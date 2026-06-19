using Academic.Domain.Students;
using FluentAssertions;
using Xunit;

namespace Academic.Tests.Students;

// ── Phase 3.1 + 3.5: Value Object + Enum + Student aggregate tests ──

public class StudentIdTests
{
    [Fact]
    public void StudentId_New_GeneratesUlidOf26Chars()
    {
        var now = DateTimeOffset.UtcNow;

        var id1 = StudentId.New(now);
        var id2 = StudentId.New(now);

        id1.Value.Length.Should().Be(26);
        id2.Value.Length.Should().Be(26);
        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void StudentId_New_TwoCallsSameTimestamp_ProduceDifferentValues()
    {
        var ts = DateTimeOffset.UtcNow;
        var id1 = StudentId.New(ts);
        var id2 = StudentId.New(ts);

        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void StudentId_LexicographicOrder_ReflectsTimeOrder()
    {
        var t1 = DateTimeOffset.UtcNow;
        var t2 = t1.AddMilliseconds(100);

        var id1 = StudentId.New(t1);
        var id2 = StudentId.New(t2);

        string.Compare(id1.Value, id2.Value, StringComparison.Ordinal).Should().BeLessThan(0);
    }
}

public class DocumentIdTests
{
    [Fact]
    public void DocumentId_Create_AcceptsAlphanumeric6To15Chars()
    {
        var (result, error) = DocumentId.TryCreate("0102030405");

        result.Should().NotBeNull();
        result!.Value.Should().Be("0102030405");
        error.Should().BeNull();
    }

    [Fact]
    public void DocumentId_Create_RejectsLessThan6Chars()
    {
        var (result, error) = DocumentId.TryCreate("AB");

        result.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public void DocumentId_Create_RejectsCharsWithSpaces()
    {
        var (result, error) = DocumentId.TryCreate("ABC DEF123");

        result.Should().BeNull();
        error.Should().NotBeNull();
    }
}

public class GuardianContactTests
{
    [Fact]
    public void GuardianContact_Create_AcceptsValidNameAndEmail()
    {
        var (result, error) = GuardianContact.TryCreate("María Gómez", "maria@example.com");

        result.Should().NotBeNull();
        result!.Name.Should().Be("María Gómez");
        result.Email.Should().Be("maria@example.com");
        error.Should().BeNull();
    }

    [Fact]
    public void GuardianContact_Create_RejectsInvalidEmail()
    {
        var (result, error) = GuardianContact.TryCreate("María Gómez", "not-an-email");

        result.Should().BeNull();
        error.Should().NotBeNull();
    }
}

public class AcademicStatusTests
{
    [Fact]
    public void AcademicStatus_HasExactlyThreeValues()
    {
        var values = Enum.GetValues<AcademicStatus>();

        values.Should().HaveCount(3);
        values.Should().Contain(AcademicStatus.Active);
        values.Should().Contain(AcademicStatus.Suspended);
        values.Should().Contain(AcademicStatus.Graduated);
    }
}

public class FinancialStatusTests
{
    [Fact]
    public void FinancialStatus_HasExactlyThreeValues()
    {
        var values = Enum.GetValues<FinancialStatus>();

        values.Should().HaveCount(3);
        values.Should().Contain(FinancialStatus.Pending);
        values.Should().Contain(FinancialStatus.Paid);
        values.Should().Contain(FinancialStatus.Overdue);
    }
}

// ── Student aggregate tests ──

public class StudentAggregateTests
{
    private static readonly DateTimeOffset ValidTimestamp = DateTimeOffset.UtcNow;

    private static Student BuildValidStudent(string? grade = null)
    {
        var docId    = DocumentId.TryCreate("0102030405").Result!;
        var guardian = GuardianContact.TryCreate("María Gómez", "maria@example.com").Result!;
        var studentId    = StudentId.New(ValidTimestamp);
        var enrollmentId = StudentId.New(ValidTimestamp.AddMilliseconds(1)).Value;
        return Student.Create(studentId, "Luis Gómez", docId, grade ?? "8vo EGB", "SCH-001", guardian, enrollmentId, DateTime.UtcNow);
    }

    [Fact]
    public void Student_Create_HappyPath_RaisesStudentEnrolledDomainEvent()
    {
        var student = BuildValidStudent();

        student.DomainEvents.Should().ContainSingle();
        student.DomainEvents.First().Should().BeOfType<Academic.Domain.Students.Events.StudentEnrolledDomainEvent>();
    }

    [Fact]
    public void Student_Create_SetsAcademicStatusActive_FinancialStatusPending()
    {
        var student = BuildValidStudent();

        student.AcademicStatus.Should().Be(AcademicStatus.Active);
        student.FinancialStatus.Should().Be(FinancialStatus.Pending);
    }

    [Fact]
    public void Student_Create_EnrollmentIdIsNotNullOrEmpty()
    {
        var student = BuildValidStudent();

        student.Enrollment.EnrollmentId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Student_Create_EmptyFullName_Fails_NoEvent()
    {
        var docId    = DocumentId.TryCreate("0102030405").Result!;
        var guardian = GuardianContact.TryCreate("María Gómez", "maria@example.com").Result!;
        var studentId    = StudentId.New(ValidTimestamp);
        var enrollmentId = StudentId.New(ValidTimestamp.AddMilliseconds(1)).Value;

        var act = () => Student.Create(studentId, "", docId, "8vo EGB", "SCH-001", guardian, enrollmentId, DateTime.UtcNow);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Student_Create_FullNameOver120Chars_Fails_NoEvent()
    {
        var docId    = DocumentId.TryCreate("0102030405").Result!;
        var guardian = GuardianContact.TryCreate("María Gómez", "maria@example.com").Result!;
        var studentId    = StudentId.New(ValidTimestamp);
        var enrollmentId = StudentId.New(ValidTimestamp.AddMilliseconds(1)).Value;
        var longName = new string('A', 121);

        var act = () => Student.Create(studentId, longName, docId, "8vo EGB", "SCH-001", guardian, enrollmentId, DateTime.UtcNow);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Student_Create_EmptyGrade_Fails_NoEvent()
    {
        var docId    = DocumentId.TryCreate("0102030405").Result!;
        var guardian = GuardianContact.TryCreate("María Gómez", "maria@example.com").Result!;
        var studentId    = StudentId.New(ValidTimestamp);
        var enrollmentId = StudentId.New(ValidTimestamp.AddMilliseconds(1)).Value;

        var act = () => Student.Create(studentId, "Luis Gómez", docId, "", "SCH-001", guardian, enrollmentId, DateTime.UtcNow);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Student_ConfirmPayment_FromPending_TransitionsToPaid_RaisesEvent()
    {
        var student = BuildValidStudent();
        student.ClearDomainEvents();

        student.ConfirmPayment(DateTime.UtcNow);

        student.FinancialStatus.Should().Be(FinancialStatus.Paid);
        student.DomainEvents.Should().ContainSingle();
        student.DomainEvents.First().Should().BeOfType<Academic.Domain.Students.Events.StudentFinancialStatusChangedDomainEvent>();
    }

    [Fact]
    public void Student_ConfirmPayment_FromOverdue_TransitionsToPaid_RaisesEvent()
    {
        var student = BuildValidStudent();
        // Force Overdue status via reflection (Overdue only set by Payments in Phase 2)
        typeof(Student)
            .GetProperty(nameof(Student.FinancialStatus))!
            .SetValue(student, FinancialStatus.Overdue);
        student.ClearDomainEvents();

        student.ConfirmPayment(DateTime.UtcNow);

        student.FinancialStatus.Should().Be(FinancialStatus.Paid);
        student.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Student_ConfirmPayment_AlreadyPaid_Idempotent_NoEvent()
    {
        var student = BuildValidStudent();
        student.ClearDomainEvents();
        student.ConfirmPayment(DateTime.UtcNow); // first call
        student.ClearDomainEvents();

        student.ConfirmPayment(DateTime.UtcNow); // second call — idempotent

        student.FinancialStatus.Should().Be(FinancialStatus.Paid);
        student.DomainEvents.Should().BeEmpty();
    }

    // ── Phase 3: MarkOverdue (ADR-063) ──

    [Fact]
    public void Student_MarkOverdue_FromPending_TransitionsToOverdue_RaisesEvent()
    {
        var student = BuildValidStudent();   // starts Pending
        student.ClearDomainEvents();

        student.MarkOverdue(DateTime.UtcNow);

        student.FinancialStatus.Should().Be(FinancialStatus.Overdue);
        student.DomainEvents.Should().ContainSingle();
        student.DomainEvents.First().Should()
            .BeOfType<Academic.Domain.Students.Events.StudentFinancialStatusChangedDomainEvent>();
    }

    [Fact]
    public void Student_MarkOverdue_AlreadyOverdue_Idempotent_NoEvent()
    {
        var student = BuildValidStudent();
        student.MarkOverdue(DateTime.UtcNow);   // Pending → Overdue
        student.ClearDomainEvents();

        student.MarkOverdue(DateTime.UtcNow);   // idempotent no-op

        student.FinancialStatus.Should().Be(FinancialStatus.Overdue);
        student.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Student_MarkOverdue_WhenPaid_Throws()
    {
        var student = BuildValidStudent();
        student.ConfirmPayment(DateTime.UtcNow);   // Pending → Paid
        student.ClearDomainEvents();

        var act = () => student.MarkOverdue(DateTime.UtcNow);

        act.Should().Throw<Exception>();
        student.FinancialStatus.Should().Be(FinancialStatus.Paid);
        student.DomainEvents.Should().BeEmpty();
    }

    // ── Phase 4: Suspend (ADR-068 — no domain event) ──

    /// <summary>ESC-90 — Active → Suspended sets status, no domain event (ADR-068).</summary>
    [Fact]
    public void Student_Suspend_FromActive_TransitionsToSuspended_NoEvent()
    {
        var student = BuildValidStudent();   // starts Active
        student.ClearDomainEvents();

        student.Suspend(DateTime.UtcNow);

        student.AcademicStatus.Should().Be(AcademicStatus.Suspended);
        student.DomainEvents.Should().BeEmpty("ADR-068: Suspend raises no domain event");
    }

    /// <summary>ESC-91 — Already Suspended is idempotent no-op, no event, no throw.</summary>
    [Fact]
    public void Student_Suspend_AlreadySuspended_IsNoOp_NoEvent()
    {
        var student = BuildValidStudent();
        typeof(Student).GetProperty(nameof(Student.AcademicStatus))!.SetValue(student, AcademicStatus.Suspended);
        student.ClearDomainEvents();

        student.Suspend(DateTime.UtcNow);   // idempotent no-op

        student.AcademicStatus.Should().Be(AcademicStatus.Suspended);
        student.DomainEvents.Should().BeEmpty();
    }

    /// <summary>ESC-92 — Graduated → DomainException, status unchanged.</summary>
    [Fact]
    public void Student_Suspend_WhenGraduated_Throws_StatusUnchanged()
    {
        var student = BuildValidStudent();
        typeof(Student).GetProperty(nameof(Student.AcademicStatus))!.SetValue(student, AcademicStatus.Graduated);
        student.ClearDomainEvents();

        var act = () => student.Suspend(DateTime.UtcNow);

        act.Should().Throw<Exception>();
        student.AcademicStatus.Should().Be(AcademicStatus.Graduated);
        student.DomainEvents.Should().BeEmpty();
    }

    // ── Phase 4: Reactivate (ADR-068 — no domain event) ──

    /// <summary>ESC-93 — Suspended → Active sets status, no domain event (ADR-068).</summary>
    [Fact]
    public void Student_Reactivate_FromSuspended_TransitionsToActive_NoEvent()
    {
        var student = BuildValidStudent();
        typeof(Student).GetProperty(nameof(Student.AcademicStatus))!.SetValue(student, AcademicStatus.Suspended);
        student.ClearDomainEvents();

        student.Reactivate(DateTime.UtcNow);

        student.AcademicStatus.Should().Be(AcademicStatus.Active);
        student.DomainEvents.Should().BeEmpty("ADR-068: Reactivate raises no domain event");
    }

    /// <summary>ESC-94 — Already Active is idempotent no-op, no event, no throw.</summary>
    [Fact]
    public void Student_Reactivate_AlreadyActive_IsNoOp_NoEvent()
    {
        var student = BuildValidStudent();   // starts Active
        student.ClearDomainEvents();

        student.Reactivate(DateTime.UtcNow);   // idempotent no-op

        student.AcademicStatus.Should().Be(AcademicStatus.Active);
        student.DomainEvents.Should().BeEmpty();
    }

    /// <summary>ESC-95 (Reactivate path) — Graduated → DomainException, status unchanged.</summary>
    [Fact]
    public void Student_Reactivate_WhenGraduated_Throws_StatusUnchanged()
    {
        var student = BuildValidStudent();
        typeof(Student).GetProperty(nameof(Student.AcademicStatus))!.SetValue(student, AcademicStatus.Graduated);
        student.ClearDomainEvents();

        var act = () => student.Reactivate(DateTime.UtcNow);

        act.Should().Throw<Exception>();
        student.AcademicStatus.Should().Be(AcademicStatus.Graduated);
        student.DomainEvents.Should().BeEmpty();
    }

    // ── Phase 4: Graduate (ADR-066 terminal, ADR-068 no event) ──

    /// <summary>ESC-95a — Active → Graduated sets status, no domain event (ADR-068).</summary>
    [Fact]
    public void Student_Graduate_FromActive_TransitionsToGraduated_NoEvent()
    {
        var student = BuildValidStudent();   // starts Active
        student.ClearDomainEvents();

        student.Graduate(DateTime.UtcNow);

        student.AcademicStatus.Should().Be(AcademicStatus.Graduated);
        student.DomainEvents.Should().BeEmpty("ADR-068: Graduate raises no domain event");
    }

    /// <summary>ESC-95b — Suspended → Graduated sets status, no domain event.</summary>
    [Fact]
    public void Student_Graduate_FromSuspended_TransitionsToGraduated_NoEvent()
    {
        var student = BuildValidStudent();
        typeof(Student).GetProperty(nameof(Student.AcademicStatus))!.SetValue(student, AcademicStatus.Suspended);
        student.ClearDomainEvents();

        student.Graduate(DateTime.UtcNow);

        student.AcademicStatus.Should().Be(AcademicStatus.Graduated);
        student.DomainEvents.Should().BeEmpty();
    }

    /// <summary>ESC-96 — Already Graduated → DomainException (TERMINAL — NOT idempotent, ADR-066).</summary>
    [Fact]
    public void Student_Graduate_WhenAlreadyGraduated_Throws()
    {
        var student = BuildValidStudent();
        typeof(Student).GetProperty(nameof(Student.AcademicStatus))!.SetValue(student, AcademicStatus.Graduated);
        student.ClearDomainEvents();

        var act = () => student.Graduate(DateTime.UtcNow);

        act.Should().Throw<Exception>("ESC-96: re-graduating is a terminal 409, not an idempotent no-op (ADR-066)");
        student.AcademicStatus.Should().Be(AcademicStatus.Graduated);
        student.DomainEvents.Should().BeEmpty();
    }
}
