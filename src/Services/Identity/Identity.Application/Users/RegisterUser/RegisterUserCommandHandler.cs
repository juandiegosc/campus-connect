using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Messaging;
using Identity.Application.Abstractions;
using Identity.Domain.Users;
using MediatR;

namespace Identity.Application.Users.RegisterUser;

/// <summary>
/// Handles <see cref="RegisterUserCommand"/>.
/// Flow: exists-check → hash → User.Create → AddAsync → return Result&lt;Guid&gt;.
/// SaveChangesAsync is NOT called here — the kernel's UnitOfWorkBehavior handles it (ADR-006).
/// Domain events are dispatched automatically by BaseDbContext after the commit (ADR-018).
/// </summary>
internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
    : IRequestHandler<RegisterUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Short-circuit if username is already taken (avoids BCrypt cost on conflict).
        var exists = await userRepository.ExistsByUsernameAsync(command.Username, cancellationToken);
        if (exists)
        {
            return Result<Guid>.Failure(
                Error.Conflict(
                    "identity.user.username_taken",
                    $"Username '{command.Username}' is already registered."));
        }

        // 2. Hash the raw password via the port (BCrypt cost 12 in Infrastructure, ADR-024).
        var hash = passwordHasher.Hash(command.Password);

        // 3. Construct the aggregate — enforces domain invariants.
        var user = User.Create(
            UserId.New(),
            command.Username,
            command.FullName,
            PasswordHash.Create(hash),
            command.Role,
            timeProvider.GetUtcNow().UtcDateTime);

        // 4. Stage the entity for persistence (no commit here).
        await userRepository.AddAsync(user, cancellationToken);

        // 5. Return the new user's ID. UnitOfWorkBehavior will commit; BaseDbContext will dispatch
        //    UserCreatedDomainEvent after the commit.
        return Result<Guid>.Success(user.Id.Value);
    }
}
