using PaymentService.Models;

namespace PaymentService.Repositories;

public interface IPaymentRepository
{
    Task<PaymentRecord?> GetPaymentByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<decimal?> GetOrderAmountAsync(int orderId, CancellationToken cancellationToken = default);
    Task<PaymentRecord> UpsertSuccessfulPaymentAsync(int orderId, decimal amount, CancellationToken cancellationToken = default);
    Task<PaymentRecord> UpsertFailedPaymentAsync(int orderId, decimal amount, string? failureReason, CancellationToken cancellationToken = default);
    Task<PaymentRecord> RefundPaymentAsync(int orderId, string? reason, CancellationToken cancellationToken = default);
}