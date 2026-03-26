namespace InventoryService.Models;

public sealed record InventoryReservationResult(
    int OrderId,
    bool Succeeded,
    string? Reason);