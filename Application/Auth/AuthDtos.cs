namespace AssetMgmt.Application.Auth;

public record LoginRequest(string UserName, string Password);

public record RefreshRequest(string RefreshToken);

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    string TokenType = "Bearer");

public record MeResponse(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    string EmployeeCode,
    string Role,
    Guid? DepartmentId,
    string? DepartmentName);
