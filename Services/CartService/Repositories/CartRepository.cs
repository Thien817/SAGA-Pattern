using System.Text.Json;
using CartService.DTOs;
using CartService.Infrastructure;
using CartService.Models;
using Microsoft.Data.SqlClient;

namespace CartService.Repositories;

public sealed class CartRepository(SqlConnectionFactory connectionFactory) : ICartRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public async Task<CheckoutResponse> CheckoutAndStageCartCheckedOutAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        try
        {
            const string findCartSql = @"
SELECT TOP 1 CartId
FROM cart.Carts
WHERE UserId = @UserId
  AND Status = 'ACTIVE'
ORDER BY CreatedAt DESC";

            int cartId;
            await using (var findCmd = new SqlCommand(findCartSql, conn, (SqlTransaction)tx))
            {
                findCmd.Parameters.AddWithValue("@UserId", userId);
                var existing = await findCmd.ExecuteScalarAsync(cancellationToken);
                if (existing is not int foundCartId)
                {
                    throw new InvalidOperationException("Cart is empty.");
                }

                cartId = foundCartId;
            }

            const string getItemsSql = @"
SELECT ci.CartItemId, ci.ProductId, ISNULL(p.ProductName, 'Unknown'), ci.Quantity, ci.UnitPrice
FROM cart.CartItems ci
LEFT JOIN inv.Products p ON p.ProductId = ci.ProductId
WHERE ci.CartId = @CartId
ORDER BY ci.CreatedAt";

            var items = new List<CartItemDto>();
            await using (var itemsCmd = new SqlCommand(getItemsSql, conn, (SqlTransaction)tx))
            {
                itemsCmd.Parameters.AddWithValue("@CartId", cartId);

                await using var reader = await itemsCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var quantity = reader.GetInt32(3);
                    var unitPrice = reader.GetDecimal(4);
                    items.Add(new CartItemDto(
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetString(2),
                        quantity,
                        unitPrice,
                        quantity * unitPrice));
                }
            }

            if (items.Count == 0)
            {
                throw new InvalidOperationException("Cart is empty.");
            }

            var totalAmount = items.Sum(x => x.LineTotal);

            const string markCheckedOutSql = @"
UPDATE cart.Carts
SET Status = 'CHECKED_OUT',
    UpdatedAt = SYSUTCDATETIME()
WHERE CartId = @CartId
  AND Status = 'ACTIVE'";

            await using (var updateCmd = new SqlCommand(markCheckedOutSql, conn, (SqlTransaction)tx))
            {
                updateCmd.Parameters.AddWithValue("@CartId", cartId);
                var rows = await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                if (rows == 0)
                {
                    throw new InvalidOperationException("Cart is not available for checkout.");
                }
            }

            var payload = new
            {
                cartId,
                userId,
                totalAmount,
                items = items.Select(x => new
                {
                    x.ProductId,
                    x.Quantity,
                    x.UnitPrice
                }).ToList()
            };

            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

            const string insertOutboxSql = @"
INSERT INTO msg.OutboxEvents (AggregateType, AggregateId, EventType, PayloadJson)
VALUES ('Cart', @AggregateId, 'CartCheckedOut', @PayloadJson)";

            await using (var outboxCmd = new SqlCommand(insertOutboxSql, conn, (SqlTransaction)tx))
            {
                outboxCmd.Parameters.AddWithValue("@AggregateId", cartId);
                outboxCmd.Parameters.AddWithValue("@PayloadJson", payloadJson);
                await outboxCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return new CheckoutResponse(cartId, userId, totalAmount, items);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
