using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Messaging;

namespace Identity.Application.Users.GetCurrentUser;

/// <summary>
/// Query to retrieve the currently authenticated user's profile from JWT claims.
/// Parameters are sourced exclusively from HttpContext.User claims at the API endpoint (ADR-028).
/// The handler performs ZERO database queries.
/// IHttpContextAccessor MUST NOT appear in Application or Domain.
/// </summary>
/// <param name="UserId">User ID extracted from <c>sub</c> claim.</param>
/// <param name="Username">Username extracted from <c>unique_name</c> claim.</param>
/// <param name="FullName">Full name extracted from <c>name</c> claim.</param>
/// <param name="Role">Role extracted from <c>role</c> claim.</param>
public sealed record GetCurrentUserQuery(
    Guid UserId,
    string Username,
    string FullName,
    string Role) : IQuery<CurrentUserResponse>;
