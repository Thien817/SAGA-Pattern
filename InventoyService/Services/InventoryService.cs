using System.Text.Json;
using InventoryService.DTOs;
using InventoryService.Repositories;

namespace InventoryService.Services;

public sealed class InventoryService(IInventoryRepository repo) : IInventoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var data = await repo.GetProductsAsync(cancellationToken);

        return data.Select(x =>
            new ProductDto(x.ProductId, x.ProductName, x.Price, x.Stock)
        ).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        var x = await repo.GetByIdAsync(productId, cancellationToken);
        if (x is null) return null;

        return new ProductDto(x.ProductId, x.ProductName, x.Price, x.Stock);
    }

    public async Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new InvalidOperationException("eventType is required.");

        if (!string.Equals(eventType, "PaymentSucceeded", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported eventType: {eventType}");

        var payload = JsonSerializer.Deserialize<PaymentSucceededPayload>(payloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid PaymentSucceeded payload.");

        var reserved = await repo.ReserveForOrderAsync(payload.OrderId, cancellationToken);

        if (reserved)
        {
            var outboxPayload = JsonSerializer.Serialize(new
            {
                payload.OrderId,
                ReservationReason = "Inventory reserved successfully"
            });

            await repo.AddOutboxEventAsync("Inventory", payload.OrderId, "InventoryReserved", outboxPayload, cancellationToken);
            return;
        }

        var failedPayload = JsonSerializer.Serialize(new
        {
            payload.OrderId,
            FailureReason = "Insufficient stock"
        });

        await repo.AddOutboxEventAsync("Inventory", payload.OrderId, "InventoryFailed", failedPayload, cancellationToken);
    }
}
