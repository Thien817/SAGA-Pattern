using AuthService.Auth;
using AuthService.DTOs;
using AuthService.Repositories;

namespace AuthService.Services;

public sealed class AuthService(IAuthRepository authRepository, JwtTokenService jwtTokenService) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await authRepository.GetUserByUserNameAsync(request.UserName);
        if (user is null || !user.IsActive) return null;

        var valid = string.Equals(user.PasswordHash, request.Password, StringComparison.Ordinal);
        if (!valid) return null;

        var accessToken = jwtTokenService.GenerateToken(user.UserId, user.UserName, user.RoleName, 60);
        var refreshToken = Guid.NewGuid().ToString("N");

        await authRepository.SaveRefreshTokenAsync(user.UserId, refreshToken, DateTime.UtcNow.AddDays(7));

        return new LoginResponse(
            user.UserId,
            user.UserName,
            user.RoleName,
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(60));
    }

    public async Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request)
    {
        var user = await authRepository.GetUserByRefreshTokenAsync(request.RefreshToken);
        if (user is null || !user.IsActive) return null;

        await authRepository.RevokeRefreshTokenAsync(request.RefreshToken);

        var newRefreshToken = Guid.NewGuid().ToString("N");
        await authRepository.SaveRefreshTokenAsync(user.UserId, newRefreshToken, DateTime.UtcNow.AddDays(7));

        var accessToken = jwtTokenService.GenerateToken(user.UserId, user.UserName, user.RoleName, 60);

        return new LoginResponse(
            user.UserId,
            user.UserName,
            user.RoleName,
            accessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(60));
    }

    public async Task LogoutAsync(string refreshToken)
    {
        await authRepository.RevokeRefreshTokenAsync(refreshToken);
    }
}
