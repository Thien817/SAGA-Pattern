using PaymentService.Models;
using PaymentService.Repositories;

namespace PaymentService.Services
{
    public class PaymentProcessor
    {
        private readonly IPaymentRepository _repository;

        public PaymentProcessor(IPaymentRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> Process(int orderId, decimal amount)
        {
            var payment = new Payment
            {
                // ❌ KHÔNG set PaymentId nữa
                OrderId = orderId,
                Amount = amount,
                Status = "PENDING",
                Provider = "VISA",
                TransactionRef = Guid.NewGuid().ToString(), // vẫn ok
                FailureReason = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };
            // 🔥 INSERT DB
            await _repository.Create(payment);

            // demo logic
            var isSuccess = amount < 1000000;

            payment.Status = isSuccess ? "SUCCESS" : "FAILED";
            payment.UpdatedAt = DateTime.UtcNow;

            if (!isSuccess)
                payment.FailureReason = "Insufficient balance";

            await _repository.Update(payment);

            return isSuccess;
        }
    }
}
