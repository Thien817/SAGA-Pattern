using PaymentService.Infrastructure;
namespace PaymentService.Repositories
{
    using Dapper;
    using PaymentService.Models;

    public class PaymentRepository : IPaymentRepository
    {
        private readonly SqlConnectionFactory _connectionFactory;

        public PaymentRepository(SqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task Create(Payment payment)
        {
            using var conn = _connectionFactory.Create();

            var sql = @"
INSERT INTO pay.Payments
(OrderId, Amount, Status, Provider, TransactionRef, FailureReason, CreatedAt, UpdatedAt)
VALUES
(@OrderId, @Amount, @Status, @Provider, @TransactionRef, @FailureReason, @CreatedAt, @UpdatedAt)
";

            await conn.ExecuteAsync(sql, payment);
        }

        public async Task Update(Payment payment)
        {
            using var conn = _connectionFactory.Create();

            var sql = @"
            UPDATE pay.Payments
            SET Status = @Status,
                FailureReason = @FailureReason,
                UpdatedAt = @UpdatedAt
            WHERE PaymentId = @PaymentId
        ";

            await conn.ExecuteAsync(sql, payment);
        }
    }
}
