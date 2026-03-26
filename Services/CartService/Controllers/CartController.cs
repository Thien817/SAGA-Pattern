using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CartService.DTOs;
using CartService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartService.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize]
public sealed class CartController(ICartService cartService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyCart()
    {
        var userId = GetUserId();
        var cart = await cartService.GetMyCartAsync(userId);
        return Ok(cart);
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest request)
    {
        if (request.ProductId == Guid.Empty || request.Quantity <= 0)
        {
            return BadRequest(new { message = "productId and quantity > 0 are required" });
        }

        try
        {
            var userId = GetUserId();
            var cart = await cartService.AddItemAsync(userId, request);
            return Ok(cart);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("items/{cartItemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid cartItemId, [FromBody] UpdateCartItemRequest request)
    {
        if (request.Quantity <= 0)
        {
            return BadRequest(new { message = "quantity > 0 is required" });
        }

        try
        {
            var userId = GetUserId();
            var cart = await cartService.UpdateItemAsync(userId, cartItemId, request);
            return Ok(cart);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("items/{cartItemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid cartItemId)
    {
        var userId = GetUserId();
        await cartService.RemoveItemAsync(userId, cartItemId);
        return NoContent();
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout()
    {
        try
        {
            var userId = GetUserId();
            var result = await cartService.CheckoutAsync(userId);

            return Ok(new
            {
                message = "CartCheckedOut published",
                eventType = "CartCheckedOut",
                payload = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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
