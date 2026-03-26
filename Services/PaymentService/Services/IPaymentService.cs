using PaymentService.DTOs;

namespace PaymentService.Services;

public interface IPaymentService
{
    Task<PaymentDto?> GetPaymentByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default);
    Task<PaymentDto> SimulateSuccessAsync(int orderId, CancellationToken cancellationToken = default);
    Task<PaymentDto> SimulateFailAsync(int orderId, string? failureReason, CancellationToken cancellationToken = default);
}