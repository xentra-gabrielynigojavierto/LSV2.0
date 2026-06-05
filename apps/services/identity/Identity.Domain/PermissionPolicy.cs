namespace Identity.Domain;

public class PermissionPolicy
{
    public Guid Id { get; private set; }
    public string PermissionCode { get; private set; } = string.Empty;
    public Guid PolicyId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public Policy Policy { get; private set; } = null!;

    private PermissionPolicy() { }

    public static PermissionPolicy Create(
        string permissionCode,
        Guid policyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        return new PermissionPolicy
        {
            Id = Guid.NewGuid(),
            PermissionCode = permissionCode.Trim(),
            PolicyId = policyId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
