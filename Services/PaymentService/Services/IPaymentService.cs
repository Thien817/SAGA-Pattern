using PaymentService.DTOs;

namespace PaymentService.Services;

public interface IPaymentService
{
    Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default);
    Task<SimulatePaymentResponse> SimulateSuccessAsync(int orderId, CancellationToken cancellationToken = default);
    Task<SimulatePaymentResponse> SimulateFailAsync(int orderId, CancellationToken cancellationToken = default);
}
