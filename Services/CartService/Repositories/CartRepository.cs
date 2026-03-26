using CartService.Infrastructure;
using CartService.Models;
using Microsoft.Data.SqlClient;

namespace CartService.Repositories;

public sealed class CartRepository(SqlConnectionFactory connectionFactory) : ICartRepository
{
    public async Task<Guid> EnsureActiveCartAsync(Guid userId)
    {
        const string findSql = @"
SELECT TOP 1 CartId
FROM cart.Carts
WHERE UserId = @UserId
  AND Status = 'ACTIVE'
ORDER BY CreatedAt DESC";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using (var findCmd = new SqlCommand(findSql, conn))
        {
            findCmd.Parameters.AddWithValue("@UserId", userId);
            var existing = await findCmd.ExecuteScalarAsync();
            if (existing is Guid cartId) return cartId;
        }

        const string insertSql = @"
INSERT INTO cart.Carts (CartId, UserId, Status, CreatedAt, UpdatedAt)
VALUES (NEWID(), @UserId, 'ACTIVE', SYSUTCDATETIME(), SYSUTCDATETIME());

SELECT TOP 1 CartId
FROM cart.Carts
WHERE UserId = @UserId
  AND Status = 'ACTIVE'
ORDER BY CreatedAt DESC";

        await using var insertCmd = new SqlCommand(insertSql, conn);
        insertCmd.Parameters.AddWithValue("@UserId", userId);
        var created = await insertCmd.ExecuteScalarAsync();
        return (Guid)created!;
    }

    public async Task<IReadOnlyList<CartItemRecord>> GetCartItemsAsync(Guid cartId)
    {
        const string sql = @"
SELECT ci.CartItemId, ci.ProductId, ISNULL(p.ProductName, 'Unknown'), ci.Quantity, ci.UnitPrice
FROM cart.CartItems ci
LEFT JOIN inv.Products p ON p.ProductId = ci.ProductId
WHERE ci.CartId = @CartId
ORDER BY ci.CreatedAt";

        var result = new List<CartItemRecord>();

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@CartId", cartId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new CartItemRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetDecimal(4)));
        }

        return result;
    }

    public async Task<ProductRecord?> GetProductAsync(Guid productId)
    {
        const string sql = @"
SELECT TOP 1 ProductId, Price
FROM inv.Products
WHERE ProductId = @ProductId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new ProductRecord(reader.GetGuid(0), reader.GetDecimal(1));
    }

    public async Task AddOrIncreaseItemAsync(Guid cartId, Guid productId, int quantity, decimal unitPrice)
    {
        const string sql = @"
IF EXISTS (SELECT 1 FROM cart.CartItems WHERE CartId = @CartId AND ProductId = @ProductId)
BEGIN
    UPDATE cart.CartItems
    SET Quantity = Quantity + @Quantity
    WHERE CartId = @CartId AND ProductId = @ProductId
END
ELSE
BEGIN
    INSERT INTO cart.CartItems (CartItemId, CartId, ProductId, Quantity, UnitPrice, CreatedAt)
    VALUES (NEWID(), @CartId, @ProductId, @Quantity, @UnitPrice, SYSUTCDATETIME())
END";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@CartId", cartId);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@Quantity", quantity);
        cmd.Parameters.AddWithValue("@UnitPrice", unitPrice);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> UpdateItemQuantityAsync(Guid cartId, Guid cartItemId, int quantity)
    {
        const string sql = @"
UPDATE cart.CartItems
SET Quantity = @Quantity
WHERE CartItemId = @CartItemId
  AND CartId = @CartId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Quantity", quantity);
        cmd.Parameters.AddWithValue("@CartItemId", cartItemId);
        cmd.Parameters.AddWithValue("@CartId", cartId);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task RemoveItemAsync(Guid cartId, Guid cartItemId)
    {
        const string sql = @"
DELETE FROM cart.CartItems
WHERE CartItemId = @CartItemId
  AND CartId = @CartId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@CartItemId", cartItemId);
        cmd.Parameters.AddWithValue("@CartId", cartId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkCheckedOutAsync(Guid cartId)
    {
        const string sql = @"
UPDATE cart.Carts
SET Status = 'CHECKED_OUT',
    UpdatedAt = SYSUTCDATETIME()
WHERE CartId = @CartId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@CartId", cartId);

        await cmd.ExecuteNonQueryAsync();
    }
}
