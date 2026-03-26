namespace PaymentService.Models;

public sealed record PaymentRecord(
    int PaymentId,
    int OrderId,
    decimal Amount,
    string Status,
    string? Provider,
    string? TransactionRef,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);