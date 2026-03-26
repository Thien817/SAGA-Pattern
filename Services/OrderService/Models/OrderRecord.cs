namespace OrderService.Models;

public sealed record OrderRecord(
    int OrderId,
    int? CartId,
    int UserId,
    decimal TotalAmount,
    string Status,
    string? CancelReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
