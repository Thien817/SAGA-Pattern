using Microsoft.AspNetCore.Mvc;
using PaymentService.DTOs;
using PaymentService.Services;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    [HttpGet("{orderId:int}")]
    public async Task<IActionResult> GetByOrderId(int orderId, CancellationToken cancellationToken)
    {
        var payment = await paymentService.GetPaymentByOrderIdAsync(orderId, cancellationToken);
        return payment is null
            ? NotFound(new { message = $"No payment found for orderId {orderId}." })
            : Ok(payment);
    }

    [HttpPost("{orderId:int}/simulate-success")]
    public async Task<IActionResult> SimulateSuccess(int orderId, CancellationToken cancellationToken)
    {
        try
        {
            var payment = await paymentService.SimulateSuccessAsync(orderId, cancellationToken);
            return Ok(new
            {
                message = "PaymentSucceeded staged to outbox",
                eventType = "PaymentSucceeded",
                payload = payment
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{orderId:int}/simulate-fail")]
    public async Task<IActionResult> SimulateFail(int orderId, [FromBody] SimulatePaymentFailureRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var payment = await paymentService.SimulateFailAsync(orderId, request?.FailureReason, cancellationToken);
            return Ok(new
            {
                message = "PaymentFailed staged to outbox",
                eventType = "PaymentFailed",
                payload = payment
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}