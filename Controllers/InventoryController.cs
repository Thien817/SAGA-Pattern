using BusinessLogicLayer.IServices;
using Microsoft.AspNetCore.Mvc;

namespace SAGA_Pattern.Controllers
{
    [ApiController]
    [Route("api/products")]
    public sealed class InventoryController(IInventoryService inventoryService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var result = await inventoryService.GetProductsAsync();
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetProduct(Guid id)
        {
            var product = await inventoryService.GetProductAsync(id);
            if (product is null)
                return NotFound();

            return Ok(product);
        }
    }
}
