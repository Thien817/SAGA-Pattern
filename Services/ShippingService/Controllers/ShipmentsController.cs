using Microsoft.AspNetCore.Mvc;
using ShippingService.Services;

namespace ShippingService.Controllers;

[ApiController]
[Route("api/shipments")]
public sealed class ShipmentsController(IShippingService shippingService) : ControllerBase
{
    [HttpGet("{orderId:int}")]
    public async Task<IActionResult> GetByOrderId(int orderId, CancellationToken ct)
    {
        var shipment = await shippingService.GetShipmentByOrderIdAsync(orderId, ct);
        if (shipment is null)
            return NotFound(new { message = $"No shipment found for orderId {orderId}." });

        return Ok(shipment);
    }
}
