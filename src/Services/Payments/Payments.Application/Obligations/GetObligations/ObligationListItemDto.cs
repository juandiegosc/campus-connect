namespace Payments.Application.Obligations.GetObligations;

public sealed record ObligationListItemDto(
    string   ObligationId,
    string   StudentId,
    string   Concept,
    decimal  Amount,
    DateTime DueDate,
    string   Status);
