using BuildingBlocks.Application.Common;
using FluentAssertions;
using Payments.Application.Abstractions;
using Payments.Application.Students.GetStudents;
using Payments.Application.Students.Shared;
using Xunit;

namespace Payments.Tests.Students;

/// <summary>
/// Unit tests for GetStudentsQueryHandler.
/// ESC-PM-42..46, REQ-PM2-06..09, REQ-PM2-11.
/// TDD: these tests drive the port + query + handler into existence.
/// </summary>
public sealed class GetStudentsQueryHandlerTests
{
    // Shared in-memory fake for all test methods in this file.
    private readonly FakeStudentReplicaRepository _repo = new();

    private GetStudentsQueryHandler CreateHandler() => new(_repo);

    // ── 1.1 RED: drives IStudentReplicaRepository + GetStudentsQuery into existence ──

    [Fact]
    public async Task Handle_EmptyRepo_ReturnsSuccessWithEmptyItems()
    {
        var result = await CreateHandler().Handle(
            new GetStudentsQuery(1, 20, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SeededItems_ReturnsCorrectCount()
    {
        _repo.Seed("STU-001", "Ana Torres",  "5A", "SCH-001");
        _repo.Seed("STU-002", "Carlos Ruiz", "6B", "SCH-001");

        var result = await CreateHandler().Handle(
            new GetStudentsQuery(1, 20, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Total.Should().Be(2);
    }

    [Fact]
    public async Task Handle_GradeFilter_NarrowsResults()
    {
        _repo.Seed("STU-001", "Ana Torres",  "5A", "SCH-001");
        _repo.Seed("STU-002", "Carlos Ruiz", "6B", "SCH-001");
        _repo.Seed("STU-003", "Maria Lopez", "5A", "SCH-001");

        var result = await CreateHandler().Handle(
            new GetStudentsQuery(1, 20, "5A", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.All(i => i.Grade == "5A").Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SearchFilter_NarrowsByFullName()
    {
        _repo.Seed("STU-001", "Ana Torres",  "5A", "SCH-001");
        _repo.Seed("STU-002", "Carlos Ruiz", "6B", "SCH-001");

        var result = await CreateHandler().Handle(
            new GetStudentsQuery(1, 20, null, "torres"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].FullName.Should().Be("Ana Torres");
    }

    // ── 2.1 RED: ExistsAsync contract ──────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_UnknownId_ReturnsFalse()
    {
        var exists = await _repo.ExistsAsync("STU-UNKNOWN", CancellationToken.None);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_KnownId_ReturnsTrue()
    {
        _repo.Seed("STU-001", "Ana Torres", "5A", "SCH-001");
        var exists = await _repo.ExistsAsync("STU-001", CancellationToken.None);
        exists.Should().BeTrue();
    }
}

// ── Shared fake ────────────────────────────────────────────────────────────────

/// <summary>
/// In-memory fake for IStudentReplicaRepository.
/// Implements ALL three port methods — update this class if the interface changes (Gotcha #185.1).
/// </summary>
internal sealed class FakeStudentReplicaRepository : IStudentReplicaRepository
{
    private readonly List<StudentReplicaItemDto> _store = [];

    public void Seed(string studentId, string fullName, string grade, string schoolId)
        => _store.Add(new StudentReplicaItemDto(studentId, fullName, grade, schoolId, DateTime.UtcNow));

    public Task UpsertAsync(string studentId, string fullName, string grade, string schoolId,
                            DateTime lastUpdatedAt, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(s => s.StudentId == studentId);
        if (idx >= 0)
            _store[idx] = new StudentReplicaItemDto(studentId, fullName, grade, schoolId, lastUpdatedAt);
        else
            _store.Add(new StudentReplicaItemDto(studentId, fullName, grade, schoolId, lastUpdatedAt));
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string studentId, CancellationToken ct = default)
        => Task.FromResult(_store.Any(s => s.StudentId == studentId));

    public Task<(IReadOnlyList<StudentReplicaItemDto> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? grade, string? search, CancellationToken ct = default)
    {
        var q = _store.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(grade))  q = q.Where(s => s.Grade == grade);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(s => s.FullName.Contains(search, StringComparison.OrdinalIgnoreCase));
        var list = q.OrderBy(s => s.StudentId).ToList();
        var total = list.Count;
        var items = (IReadOnlyList<StudentReplicaItemDto>)list
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((items, total));
    }
}
