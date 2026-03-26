using System.Text.Json;
using PaymentService.DTOs;
using PaymentService.Models;
using PaymentService.Repositories;

namespace PaymentService.Services;

public sealed class PaymentService(IPaymentRepository paymentRepository) : IPaymentService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<PaymentDto?> GetPaymentByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var payment = await paymentRepository.GetPaymentByOrderIdAsync(orderId, cancellationToken);
        return payment is null ? null : MapToDto(payment);
    }

    public async Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new InvalidOperationException("eventType is required.");
        }

        switch (eventType)
        {
            case "OrderCreated":
            {
                var payload = JsonSerializer.Deserialize<OrderCreatedPayload>(payloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("Invalid OrderCreated payload.");
                await paymentRepository.UpsertSuccessfulPaymentAsync(payload.OrderId, payload.TotalAmount, cancellationToken);
                return;
            }
            case "InventoryFailed":
            {
                var payload = JsonSerializer.Deserialize<InventoryFailedPayload>(payloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("Invalid InventoryFailed payload.");
                await paymentRepository.RefundPaymentAsync(payload.OrderId, payload.FailureReason, cancellationToken);
                return;
            }
            default:
                throw new InvalidOperationException($"Unsupported eventType: {eventType}");
        }
    }

    public async Task<PaymentDto> SimulateSuccessAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var amount = await ResolveAmountAsync(orderId, cancellationToken);
        var payment = await paymentRepository.UpsertSuccessfulPaymentAsync(orderId, amount, cancellationToken);
        return MapToDto(payment);
    }

    public async Task<PaymentDto> SimulateFailAsync(int orderId, string? failureReason, CancellationToken cancellationToken = default)
    {
        var amount = await ResolveAmountAsync(orderId, cancellationToken);
        var payment = await paymentRepository.UpsertFailedPaymentAsync(orderId, amount, failureReason, cancellationToken);
        return MapToDto(payment);
    }

    private async Task<decimal> ResolveAmountAsync(int orderId, CancellationToken cancellationToken)
    {
        var existingPayment = await paymentRepository.GetPaymentByOrderIdAsync(orderId, cancellationToken);
        if (existingPayment is not null)
        {
            return existingPayment.Amount;
        }

        var orderAmount = await paymentRepository.GetOrderAmountAsync(orderId, cancellationToken);
        if (orderAmount is null)
        {
            throw new InvalidOperationException($"OrderId {orderId} was not found.");
        }

        return orderAmount.Value;
    }

    private static PaymentDto MapToDto(PaymentRecord record)
    {
        return new PaymentDto(
            record.PaymentId,
            record.OrderId,
            record.Amount,
            record.Status,
            record.Provider,
            record.TransactionRef,
            record.FailureReason,
            record.CreatedAt,
            record.UpdatedAt);
    }
}