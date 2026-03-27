using CartService.Infrastructure;
using CartService.Models;
using Microsoft.Data.SqlClient;

namespace CartService.Repositories;

public sealed class CartRepository(SqlConnectionFactory connectionFactory) : ICartRepository
{
    public async Task<int> EnsureActiveCartAsync(int userId)
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
            if (existing is int cartId) return cartId;
        }

        const string insertSql = @"
INSERT INTO cart.Carts (UserId, Status, CreatedAt, UpdatedAt)
VALUES (@UserId, 'ACTIVE', SYSUTCDATETIME(), SYSUTCDATETIME());

SELECT TOP 1 CartId
FROM cart.Carts
WHERE UserId = @UserId
  AND Status = 'ACTIVE'
ORDER BY CreatedAt DESC";

        await using var insertCmd = new SqlCommand(insertSql, conn);
        insertCmd.Parameters.AddWithValue("@UserId", userId);
        var created = await insertCmd.ExecuteScalarAsync();
        return (int)created!;
    }

    public async Task<IReadOnlyList<CartItemRecord>> GetCartItemsAsync(int cartId)
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
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetDecimal(4)));
        }

        return result;
    }

    public async Task<ProductRecord?> GetProductAsync(int productId)
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

        return new ProductRecord(reader.GetInt32(0), reader.GetDecimal(1));
    }

    public async Task AddOrIncreaseItemAsync(int cartId, int productId, int quantity, decimal unitPrice)
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
    INSERT INTO cart.CartItems (CartId, ProductId, Quantity, UnitPrice, CreatedAt)
    VALUES (@CartId, @ProductId, @Quantity, @UnitPrice, SYSUTCDATETIME())
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

    public async Task<bool> UpdateItemQuantityAsync(int cartId, int cartItemId, int quantity)
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

    public async Task RemoveItemAsync(int cartId, int cartItemId)
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

    public async Task MarkCheckedOutAsync(int cartId)
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

    public async Task AddOutboxEventAsync(string aggregateType, int aggregateId, string eventType, string payloadJson)
    {
        const string sql = @"
INSERT INTO msg.OutboxEvents (AggregateType, AggregateId, EventType, PayloadJson, PublishStatus, RetryCount)
VALUES (@AggregateType, @AggregateId, @EventType, @PayloadJson, 'PENDING', 0);";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AggregateType", aggregateType);
        cmd.Parameters.AddWithValue("@AggregateId", aggregateId);
        cmd.Parameters.AddWithValue("@EventType", eventType);
        cmd.Parameters.AddWithValue("@PayloadJson", payloadJson);

        await cmd.ExecuteNonQueryAsync();
    }
}
