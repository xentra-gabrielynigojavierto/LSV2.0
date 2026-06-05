using BuildingBlocks.Domain;
using Liens.Domain.ValueObjects;

namespace Liens.Domain.Entities;

public class Facility : AuditableEntity
{
    public Guid Id           { get; private set; }
    public Guid TenantId     { get; private set; }
    public Guid OrgId        { get; private set; }

    public string Name       { get; private set; } = string.Empty;
    public string? Code      { get; private set; }
    public string? ExternalReference { get; private set; }

    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? City         { get; private set; }
    public string? State        { get; private set; }
    public string? PostalCode   { get; private set; }

    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Fax   { get; private set; }

    public bool IsActive { get; private set; }

    public Guid? OrganizationId { get; private set; }

    private Facility() { }

    public static Facility Create(
        Guid tenantId,
        Guid orgId,
        string name,
        Guid createdByUserId,
        string? code = null,
        string? externalReference = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? phone = null,
        string? email = null,
        string? fax = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = DateTime.UtcNow;
        return new Facility
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            OrgId             = orgId,
            Name              = name.Trim(),
            Code              = code?.Trim(),
            ExternalReference = externalReference?.Trim(),
            AddressLine1      = addressLine1?.Trim(),
            AddressLine2      = addressLine2?.Trim(),
            City              = city?.Trim(),
            State             = state?.Trim(),
            PostalCode        = postalCode?.Trim(),
            Phone             = phone?.Trim(),
            Email             = email?.Trim(),
            Fax               = fax?.Trim(),
            IsActive          = true,
            CreatedByUserId   = createdByUserId,
            UpdatedByUserId   = createdByUserId,
            CreatedAtUtc      = now,
            UpdatedAtUtc      = now,
        };
    }

    public void Update(
        string name,
        Guid updatedByUserId,
        string? code = null,
        string? externalReference = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? phone = null,
        string? email = null,
        string? fax = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name              = name.Trim();
        Code              = code?.Trim();
        ExternalReference = externalReference?.Trim();
        AddressLine1      = addressLine1?.Trim();
        AddressLine2      = addressLine2?.Trim();
        City              = city?.Trim();
        State             = state?.Trim();
        PostalCode        = postalCode?.Trim();
        Phone             = phone?.Trim();
        Email             = email?.Trim();
        Fax               = fax?.Trim();
        UpdatedByUserId   = updatedByUserId;
        UpdatedAtUtc      = DateTime.UtcNow;
    }

    public void LinkOrganization(Guid organizationId, Guid updatedByUserId)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        OrganizationId  = organizationId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId)
    {
        IsActive        = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Reactivate(Guid updatedByUserId)
    {
        IsActive        = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
