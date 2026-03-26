namespace PaymentService.DTOs;

public sealed record PaymentDto(
    int PaymentId,
    int OrderId,
    decimal Amount,
    string Status,
    string? Provider,
    string? TransactionRef,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record OrderCreatedPayload(int OrderId, int CartId, int UserId, decimal TotalAmount);
public sealed record InventoryFailedPayload(int OrderId, string? FailureReason);
public sealed record PaymentSucceededPayload(int OrderId, decimal Amount, string? Provider, string? TransactionRef);
public sealed record PaymentFailedPayload(int OrderId, string? FailureReason);
public sealed record SimulatePaymentFailureRequest(string? FailureReason);