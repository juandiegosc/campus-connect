using BuildingBlocks.Application.Messaging;

namespace Payments.Application.Obligations.RegisterObligation;

/// <summary>
/// Command to register a new Obligation in Pending status.
/// ICommand&lt;T&gt; marker ensures UnitOfWorkBehavior activates (Gotcha 16).
/// </summary>
public sealed record RegisterObligationCommand(
    string   StudentId,
    string   Concept,
    decimal  Amount,
    DateTime DueDate
) : ICommand<RegisterObligationResponse>;
