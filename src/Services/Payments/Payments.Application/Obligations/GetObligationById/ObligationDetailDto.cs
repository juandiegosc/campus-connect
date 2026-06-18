namespace Payments.Application.Obligations.GetObligationById;

public sealed record ObligationDetailDto(
    string      ObligationId,
    string      StudentId,
    string      Concept,
    decimal     Amount,
    DateTime    DueDate,
    string      SchoolId,
    string      Status,
    PaymentDto? Payment);
