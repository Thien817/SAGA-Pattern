using AuthService.DTOs;

namespace AuthService.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request);
    Task LogoutAsync(string refreshToken);
}
