namespace Identity.Application.Users.GetCurrentUser;

/// <summary>
/// DTO returned by <c>GetCurrentUserQuery</c> on success.
/// All fields sourced from JWT claims — no database roundtrip (ADR-028).
/// </summary>
/// <param name="UserId">User's unique identifier (from <c>sub</c> claim).</param>
/// <param name="Username">Username (from <c>unique_name</c> claim).</param>
/// <param name="FullName">Display name (from <c>name</c> claim).</param>
/// <param name="Role">Role string (from <c>role</c> claim).</param>
public sealed record CurrentUserResponse(
    Guid UserId,
    string Username,
    string FullName,
    string Role);
