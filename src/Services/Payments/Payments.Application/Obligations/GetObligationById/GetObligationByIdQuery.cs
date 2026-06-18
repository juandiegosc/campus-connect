using BuildingBlocks.Application.Messaging;

namespace Payments.Application.Obligations.GetObligationById;

/// <summary>
/// Query to retrieve a single Obligation by id.
/// IQuery&lt;ObligationDetailDto&gt; maps to IRequest&lt;Result&lt;ObligationDetailDto&gt;&gt; via kernel (Gotcha 25 — no double-wrap).
/// </summary>
public sealed record GetObligationByIdQuery(string ObligationId)
    : IQuery<ObligationDetailDto>;
