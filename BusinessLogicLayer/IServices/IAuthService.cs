using BusinessLogicLayer.DTOs;

namespace BusinessLogicLayer.IServices;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request);
    Task LogoutAsync(string refreshToken);
}
