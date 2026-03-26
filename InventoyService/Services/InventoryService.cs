using System.Text.Json;
using InventoryService.DTOs;
using InventoryService.Repositories;

namespace InventoryService.Services;

public sealed class InventoryService(IInventoryRepository repo)
    : IInventoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync()
    {
        var data = await repo.GetProductsAsync();

        return data.Select(x =>
            new ProductDto(x.ProductId, x.ProductName, x.Price, x.Stock)
        ).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(int productId)
    {
        var x = await repo.GetByIdAsync(productId);
        if (x is null) return null;

        return new ProductDto(x.ProductId, x.ProductName, x.Price, x.Stock);
    }

    public Task<bool> ReserveAsync(int productId, int quantity)
    {
        return repo.ReserveStockAsync(productId, quantity);
    }

    public async Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new InvalidOperationException("eventType is required.");
        }

        switch (eventType)
        {
            case "PaymentSucceeded":
            {
                var payload = JsonSerializer.Deserialize<PaymentSucceededPayload>(payloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("Invalid PaymentSucceeded payload.");
                await repo.ReserveOrderAsync(payload.OrderId, cancellationToken);
                return;
            }
            default:
                throw new InvalidOperationException($"Unsupported eventType: {eventType}");
        }
    }
}