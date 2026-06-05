using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class ServiceOffering : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DurationMinutes { get; private set; }
    public bool IsActive { get; private set; }

    private ServiceOffering() { }

    public static ServiceOffering Create(
        Guid tenantId,
        string name,
        string code,
        string? description,
        int durationMinutes,
        bool isActive,
        Guid? createdByUserId)
    {
        return new ServiceOffering
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Code = code.Trim().ToUpper(),
            Description = description?.Trim(),
            DurationMinutes = durationMinutes,
            IsActive = isActive,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        string code,
        string? description,
        int durationMinutes,
        bool isActive,
        Guid? updatedByUserId)
    {
        Name = name.Trim();
        Code = code.Trim().ToUpper();
        Description = description?.Trim();
        DurationMinutes = durationMinutes;
        IsActive = isActive;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
