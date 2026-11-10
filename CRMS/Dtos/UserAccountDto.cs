namespace CRMS.Dtos;

public sealed record UserAccountDto(long UserId, string FullName, string Email, string? Phone, string UserRole, bool IsActive, DateTime CreatedAt, byte[] RowVersion);
public sealed record ProfileUpdateRequest(string FullName, string? Phone, byte[] RowVersion);
