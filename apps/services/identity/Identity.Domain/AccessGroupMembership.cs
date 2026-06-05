namespace Identity.Domain;

public class AccessGroupMembership
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public MembershipStatus MembershipStatus { get; private set; }
    public DateTime AddedAtUtc { get; private set; }
    public DateTime? RemovedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private AccessGroupMembership() { }

    public static AccessGroupMembership Create(
        Guid tenantId,
        Guid groupId,
        Guid userId,
        Guid? createdByUserId = null)
    {
        var now = DateTime.UtcNow;
        return new AccessGroupMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GroupId = groupId,
            UserId = userId,
            MembershipStatus = MembershipStatus.Active,
            AddedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId
        };
    }

    public void Remove(Guid? updatedByUserId = null)
    {
        MembershipStatus = MembershipStatus.Removed;
        RemovedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }
}
