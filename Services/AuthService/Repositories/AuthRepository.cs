using AuthService.Infrastructure;
using AuthService.Models;
using Microsoft.Data.SqlClient;

namespace AuthService.Repositories;

public sealed class AuthRepository(SqlConnectionFactory connectionFactory) : IAuthRepository
{
    public async Task<UserRecord?> GetUserByUserNameAsync(string userName)
    {
        const string sql = @"
SELECT TOP 1 UserId, UserName, PasswordHash, RoleName, IsActive
FROM auth.Users
WHERE UserName = @UserName";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserName", userName);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new UserRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4));
    }

    public async Task<UserRecord?> GetUserByRefreshTokenAsync(string refreshToken)
    {
        const string sql = @"
SELECT TOP 1 u.UserId, u.UserName, u.PasswordHash, u.RoleName, u.IsActive
FROM auth.RefreshTokens rt
JOIN auth.Users u ON u.UserId = rt.UserId
WHERE rt.Token = @Token
  AND rt.IsRevoked = 0
  AND rt.ExpiresAt > SYSUTCDATETIME()";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Token", refreshToken);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new UserRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4));
    }

    public async Task SaveRefreshTokenAsync(Guid userId, string token, DateTime expiresAtUtc)
    {
        const string sql = @"
INSERT INTO auth.RefreshTokens (RefreshTokenId, UserId, Token, ExpiresAt, IsRevoked, CreatedAt)
VALUES (NEWID(), @UserId, @Token, @ExpiresAt, 0, SYSUTCDATETIME())";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Token", token);
        cmd.Parameters.AddWithValue("@ExpiresAt", expiresAtUtc);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RevokeRefreshTokenAsync(string token)
    {
        const string sql = @"
UPDATE auth.RefreshTokens
SET IsRevoked = 1,
    RevokedAt = SYSUTCDATETIME()
WHERE Token = @Token
  AND IsRevoked = 0";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Token", token);

        await cmd.ExecuteNonQueryAsync();
    }
}
