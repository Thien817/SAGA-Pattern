using InventoryService.Infrastructure;
using InventoryService.Models;
using Microsoft.Data.SqlClient;

namespace InventoryService.Repositories;

public sealed class InventoryRepository(SqlConnectionFactory factory)
    : IInventoryRepository
{
    public async Task<IReadOnlyList<ProductRecord>> GetProductsAsync()
    {
        const string sql = @"
SELECT ProductId, ProductName, Price, Stock
FROM inv.Products";

        var result = new List<ProductRecord>();

        await using var conn = factory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new ProductRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetInt32(3)
            ));
        }

        return result;
    }

    public async Task<ProductRecord?> GetByIdAsync(int productId)
    {
        const string sql = @"
SELECT TOP 1 ProductId, ProductName, Price, Stock
FROM inv.Products
WHERE ProductId = @ProductId";

        await using var conn = factory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new ProductRecord(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetInt32(3)
        );
    }

    public async Task<bool> ReserveStockAsync(int productId, int quantity)
    {
        const string sql = @"
UPDATE inv.Products
SET Stock = Stock - @Quantity
WHERE ProductId = @ProductId
  AND Stock >= @Quantity";

        await using var conn = factory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@Quantity", quantity);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }
}