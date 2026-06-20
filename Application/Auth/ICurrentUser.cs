namespace AssetMgmt.Application.Auth;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? Id { get; }
    string? UserName { get; }
    string? Role { get; }
}
