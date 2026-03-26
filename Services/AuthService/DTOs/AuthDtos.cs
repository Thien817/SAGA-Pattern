namespace AuthService.DTOs;

public sealed record LoginRequest(string UserName, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LoginResponse(int UserId, string UserName, string Role, string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
