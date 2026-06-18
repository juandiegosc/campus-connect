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
}
