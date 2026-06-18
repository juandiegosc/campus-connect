using BuildingBlocks.Application.Messaging;

namespace Payments.Application.Obligations.GetObligations;

/// <summary>
/// Query to list obligations filtered by status.
/// IQuery&lt;T&gt; = IRequest&lt;Result&lt;T&gt;&gt; via kernel — handler implements IRequestHandler&lt;Q, Result&lt;T&gt;&gt;.
/// Do NOT wrap T in Result here — that double-wraps to Result&lt;Result&lt;T&gt;&gt; (Gotcha 25).
/// </summary>
public sealed record GetObligationsQuery(string? Status)
    : IQuery<IReadOnlyList<ObligationListItemDto>>;
