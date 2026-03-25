using System.Text.Json;
using BusinessLogicLayer.DTOs;
using BusinessLogicLayer.IServices;
using DataAccessLayer.IRepositories;
using DataAccessLayer.Models;

namespace BusinessLogicLayer.Services;

public sealed class OrderService(IOrderRepository orderRepository) : IOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<OrderDto>> GetMyOrdersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orders = await orderRepository.GetOrdersWithItemsAsync(userId, cancellationToken);
        return orders
            .Select(o => MapToDto(o))
            .ToList();
    }

    public async Task<OrderDto?> GetOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.GetOrderWithItemsAsync(orderId, userId, cancellationToken);
        return order is null ? null : MapToDto(order);
    }

    public async Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new InvalidOperationException("eventType is required.");
        }

        switch (eventType)
        {
            case "CartCheckedOut":
            {
                var payload = JsonSerializer.Deserialize<CartCheckedOutPayload>(payloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("Invalid CartCheckedOut payload.");

                var items = payload.Items
                    .Select(x => new OrderItemCreate(x.ProductId, x.Quantity, x.UnitPrice))
                    .ToList();

                await orderRepository.CreateOrReplacePendingOrderAsync(
                    payload.CartId,
                    payload.UserId,
                    payload.TotalAmount,
                    items,
                    cancellationToken);
                return;
            }

            case "PaymentFailed":
            {
                var payload = JsonSerializer.Deserialize<PaymentFailedPayload>(payloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("Invalid PaymentFailed payload.");

                await orderRepository.CancelOrderAsync(payload.OrderId, payload.FailureReason, cancellationToken);
                return;
            }

            case "InventoryFailed":
            {
                var payload = JsonSerializer.Deserialize<InventoryFailedPayload>(payloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("Invalid InventoryFailed payload.");

                await orderRepository.CancelOrderAsync(payload.OrderId, payload.FailureReason, cancellationToken);
                return;
            }

            case "InventoryReserved":
            {
                var payload = JsonSerializer.Deserialize<InventoryReservedPayload>(payloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("Invalid InventoryReserved payload.");

                await orderRepository.CompleteOrderAsync(payload.OrderId, cancellationToken);
                return;
            }

            default:
                throw new InvalidOperationException($"Unsupported eventType: {eventType}");
        }
    }

    private static OrderDto MapToDto(DataAccessLayer.Models.OrderWithItemsRecord orderWithItems)
    {
        return new OrderDto(
            orderWithItems.Order.OrderId,
            orderWithItems.Order.CartId,
            orderWithItems.Order.UserId,
            orderWithItems.Order.TotalAmount,
            orderWithItems.Order.Status,
            orderWithItems.Order.CancelReason,
            orderWithItems.Items
                .Select(x => new OrderItemDto(x.OrderItemId, x.ProductId, x.Quantity, x.UnitPrice, x.LineTotal))
                .ToList());
    }
}

