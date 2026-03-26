namespace AuthService.Models;

public sealed record UserRecord(
    int UserId,
    string UserName,
    string PasswordHash,
    string RoleName,
    bool IsActive
);
