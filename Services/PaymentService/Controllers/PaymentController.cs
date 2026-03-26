using Microsoft.AspNetCore.Mvc;
using PaymentService.Services;

namespace PaymentService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly PaymentProcessor _paymentService;

        public PaymentController(PaymentProcessor paymentService)
        {
            _paymentService = paymentService;
        }

        // ✅ TEST PAYMENT DIRECT
        [HttpPost("test")]
        public async Task<IActionResult> TestPayment([FromBody] TestPaymentRequest request)
        {
            var result = await _paymentService.Process(request.OrderId, request.Amount);

            return Ok(new
            {
                success = result,
                message = result ? "Payment success" : "Payment failed"
            });
        }
    }

    public class TestPaymentRequest
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
    }
}