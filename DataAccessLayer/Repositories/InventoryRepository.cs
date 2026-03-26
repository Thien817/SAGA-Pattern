using DataAccessLayer.IRepositories;
using DataAccessLayer.Infrastructure;
using DataAccessLayer.Models;
using Microsoft.Data.SqlClient;

namespace DataAccessLayer.Repositories;

public sealed class InventoryRepository(SqlConnectionFactory connectionFactory) : IInventoryRepository
{
    public async Task<IReadOnlyList<ProductDetailRecord>> GetProductsAsync()
    {
        const string sql = @"
SELECT ProductId, ProductName, Price, Stock
FROM inv.Products";

        var result = new List<ProductDetailRecord>();

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new ProductDetailRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetInt32(3)
            ));
        }

        return result;
    }

    public async Task<ProductDetailRecord?> GetProductByIdAsync(Guid productId)
    {
        const string sql = @"
SELECT TOP 1 ProductId, ProductName, Price, Stock
FROM inv.Products
WHERE ProductId = @ProductId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new ProductDetailRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetInt32(3)
        );
    }

    public async Task<bool> ReserveStockAsync(Guid productId, int quantity)
    {
        const string sql = @"
UPDATE inv.Products
SET Stock = Stock - @Quantity
WHERE ProductId = @ProductId
  AND Stock >= @Quantity";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@Quantity", quantity);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }
}