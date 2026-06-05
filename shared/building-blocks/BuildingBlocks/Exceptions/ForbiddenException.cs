namespace BuildingBlocks.Exceptions;

public class ForbiddenException : Exception
{
    public string? PermissionCode { get; }

    public ForbiddenException() : base("Access denied.") { }

    public ForbiddenException(string permissionCode)
        : base($"Missing permission: {permissionCode}")
    {
        PermissionCode = permissionCode;
    }
}
