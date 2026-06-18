using BuildingBlocks.Domain.Events;

namespace Identity.Domain.Users.Events;

/// <summary>
/// Domain event raised when a new <see cref="User"/> aggregate is created.
/// No consumer handler exists in Phase 2 (the event is dispatched and discarded as a no-op).
/// Future phases may add handlers for audit trails or welcome notifications.
/// </summary>
/// <param name="UserId">The identifier of the newly created user.</param>
/// <param name="Username">The username assigned to the new user.</param>
/// <param name="Role">The role assigned to the new user.</param>
/// <param name="OccurredAt">UTC timestamp of the creation event.</param>
public sealed record UserCreatedDomainEvent(
    UserId UserId,
    string Username,
    UserRole Role,
    DateTime OccurredAt) : IDomainEvent;
