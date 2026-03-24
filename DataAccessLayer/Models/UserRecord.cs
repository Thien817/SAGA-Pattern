namespace DataAccessLayer.Models;

public sealed record UserRecord(
    Guid UserId,
    string UserName,
    string PasswordHash,
    string RoleName,
    bool IsActive
);
