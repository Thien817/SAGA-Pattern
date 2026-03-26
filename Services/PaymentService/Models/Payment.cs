namespace PaymentService.Models
{
    public class Payment
    {
        public int PaymentId { get; set; } 
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public string Provider { get; set; }
        public string TransactionRef { get; set; }
        public string FailureReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
