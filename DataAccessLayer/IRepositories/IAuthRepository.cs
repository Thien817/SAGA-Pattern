using DataAccessLayer.Models;

namespace DataAccessLayer.IRepositories;

public interface IAuthRepository
{
    Task<UserRecord?> GetUserByUserNameAsync(string userName);
    Task<UserRecord?> GetUserByRefreshTokenAsync(string refreshToken);
    Task SaveRefreshTokenAsync(Guid userId, string token, DateTime expiresAtUtc);
    Task RevokeRefreshTokenAsync(string token);
}
