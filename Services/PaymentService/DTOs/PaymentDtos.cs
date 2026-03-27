namespace PaymentService.DTOs;

public sealed record SimulatePaymentResponse(int OrderId, decimal Amount, string Status, string? FailureReason);

public sealed record OrderCreatedPayload(int OrderId, decimal TotalAmount);
public sealed record InventoryFailedPayload(int OrderId, string? FailureReason);
