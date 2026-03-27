using PaymentService.Models;

namespace PaymentService.Repositories;

public interface IPaymentRepository
{
    Task<PaymentRecord> UpsertPaymentResultAsync(int orderId, decimal amount, string status, string? failureReason, CancellationToken cancellationToken = default);
    Task<PaymentRecord?> GetPaymentByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<PaymentRecord?> MarkRefundedAsync(int orderId, string? reason, CancellationToken cancellationToken = default);
    Task AddRefundAsync(int paymentId, int orderId, decimal amount, string? reason, CancellationToken cancellationToken = default);
    Task AddOutboxEventAsync(string aggregateType, int aggregateId, string eventType, string payloadJson, CancellationToken cancellationToken = default);
}
