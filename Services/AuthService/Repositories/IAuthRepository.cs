using AuthService.Models;

namespace AuthService.Repositories;

public interface IAuthRepository
{
    Task<UserRecord?> GetUserByUserNameAsync(string userName);
    Task<UserRecord?> GetUserByRefreshTokenAsync(string refreshToken);
    Task SaveRefreshTokenAsync(int userId, string token, DateTime expiresAtUtc);
    Task RevokeRefreshTokenAsync(string token);
}
