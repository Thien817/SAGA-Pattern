using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using OrderService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrderController(IOrderService orderService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = GetUserId();
        var orders = await orderService.GetMyOrdersAsync(userId, HttpContext.RequestAborted);
        return Ok(orders);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        var userId = GetUserId();
        var order = await orderService.GetOrderAsync(orderId, userId, HttpContext.RequestAborted);
        return order is null ? NotFound(new { message = "order not found" }) : Ok(order);
    }

    private Guid GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (id is null || !Guid.TryParse(id, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid token subject.");
        }

        return userId;
    }
}
