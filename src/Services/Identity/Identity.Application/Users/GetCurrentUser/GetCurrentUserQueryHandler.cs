using BuildingBlocks.Application.Common;
using MediatR;

namespace Identity.Application.Users.GetCurrentUser;

/// <summary>
/// Handles <see cref="GetCurrentUserQuery"/>.
/// Wraps claim parameters into <see cref="CurrentUserResponse"/> with ZERO repository calls (ADR-028, ESC-46).
/// IHttpContextAccessor does NOT appear here — claims arrive as primitive parameters from the API endpoint.
/// </summary>
internal sealed class GetCurrentUserQueryHandler
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    public Task<Result<CurrentUserResponse>> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        // Zero repository calls — all data sourced from JWT claims (ADR-028, ESC-46).
        var response = new CurrentUserResponse(
            query.UserId,
            query.Username,
            query.FullName,
            query.Role);

        return Task.FromResult(Result<CurrentUserResponse>.Success(response));
    }
}
