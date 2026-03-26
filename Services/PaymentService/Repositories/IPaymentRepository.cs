using PaymentService.Models;

namespace PaymentService.Repositories
{
    public interface IPaymentRepository
    {
        Task Create(Payment payment);
        Task Update(Payment payment);
    }
}
