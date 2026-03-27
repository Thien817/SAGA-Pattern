using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Services;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    [HttpPost("{orderId:int}/simulate-success")]
    public async Task<IActionResult> SimulateSuccess(int orderId)
    {
        try
        {
            var result = await paymentService.SimulateSuccessAsync(orderId, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{orderId:int}/simulate-fail")]
    public async Task<IActionResult> SimulateFail(int orderId)
    {
        try
        {
            var result = await paymentService.SimulateFailAsync(orderId, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
