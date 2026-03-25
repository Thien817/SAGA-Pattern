namespace DataAccessLayer.Models;

public sealed record OrderRecord(
    Guid OrderId,
    Guid? CartId,
    Guid UserId,
    decimal TotalAmount,
    string Status,
    string? CancelReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

