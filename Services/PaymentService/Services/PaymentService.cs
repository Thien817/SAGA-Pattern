using System.Text.Json;
using PaymentService.DTOs;
using PaymentService.Repositories;

namespace PaymentService.Services;

public sealed class PaymentService(IPaymentRepository repo, IConfiguration configuration) : IPaymentService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new InvalidOperationException("eventType is required.");

        switch (eventType)
        {
            case "OrderCreated":
            {
                var payload = JsonSerializer.Deserialize<OrderCreatedPayload>(payloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("Invalid OrderCreated payload.");

                var shouldFail = configuration.GetValue<bool>("Payment:AutoFailOnOrderCreated");
                var status = shouldFail ? "FAILED" : "SUCCESS";
                var failureReason = shouldFail ? "Auto-failed by Payment:AutoFailOnOrderCreated" : null;

                var result = await repo.UpsertPaymentResultAsync(
                    payload.OrderId,
                    payload.TotalAmount,
                    status,
                    failureReason,
                    cancellationToken);

                var outboxPayload = JsonSerializer.Serialize(new
                {
                    result.OrderId,
                    result.Amount,
                    PaymentId = result.PaymentId,
                    Status = result.Status,
                    FailureReason = result.FailureReason
                });

                var outboxEventType = shouldFail ? "PaymentFailed" : "PaymentSucceeded";
                await repo.AddOutboxEventAsync("Payment", result.PaymentId, outboxEventType, outboxPayload, cancellationToken);
                return;
            }
            case "InventoryFailed":
            {
                var payload = JsonSerializer.Deserialize<InventoryFailedPayload>(payloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("Invalid InventoryFailed payload.");

                var refunded = await repo.MarkRefundedAsync(payload.OrderId, payload.FailureReason, cancellationToken);
                if (refunded is null)
                {
                    return;
                }

                await repo.AddRefundAsync(refunded.PaymentId, refunded.OrderId, refunded.Amount, payload.FailureReason, cancellationToken);

                var outboxPayload = JsonSerializer.Serialize(new
                {
                    refunded.OrderId,
                    refunded.Amount,
                    PaymentId = refunded.PaymentId,
                    refunded.Status,
                    Reason = payload.FailureReason
                });

                await repo.AddOutboxEventAsync("Payment", refunded.PaymentId, "PaymentRefunded", outboxPayload, cancellationToken);
                return;
            }
            default:
                throw new InvalidOperationException($"Unsupported eventType: {eventType}");
        }
    }

    public async Task<SimulatePaymentResponse> SimulateSuccessAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var existing = await repo.GetPaymentByOrderIdAsync(orderId, cancellationToken)
                       ?? throw new InvalidOperationException("Payment does not exist. Create it via OrderCreated event first.");

        var result = await repo.UpsertPaymentResultAsync(orderId, existing.Amount, "SUCCESS", null, cancellationToken);

        var outboxPayload = JsonSerializer.Serialize(new
        {
            result.OrderId,
            result.Amount,
            PaymentId = result.PaymentId,
            Status = result.Status
        });

        await repo.AddOutboxEventAsync("Payment", result.PaymentId, "PaymentSucceeded", outboxPayload, cancellationToken);

        return new SimulatePaymentResponse(result.OrderId, result.Amount, result.Status, result.FailureReason);
    }

    public async Task<SimulatePaymentResponse> SimulateFailAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var existing = await repo.GetPaymentByOrderIdAsync(orderId, cancellationToken)
                       ?? throw new InvalidOperationException("Payment does not exist. Create it via OrderCreated event first.");

        const string failureReason = "Simulated payment failure";
        var result = await repo.UpsertPaymentResultAsync(orderId, existing.Amount, "FAILED", failureReason, cancellationToken);

        var outboxPayload = JsonSerializer.Serialize(new
        {
            result.OrderId,
            result.Amount,
            PaymentId = result.PaymentId,
            Status = result.Status,
            FailureReason = result.FailureReason
        });

        await repo.AddOutboxEventAsync("Payment", result.PaymentId, "PaymentFailed", outboxPayload, cancellationToken);

        return new SimulatePaymentResponse(result.OrderId, result.Amount, result.Status, result.FailureReason);
    }
}
