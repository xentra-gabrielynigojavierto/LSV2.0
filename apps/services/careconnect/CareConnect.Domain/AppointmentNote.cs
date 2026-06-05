using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class AppointmentNote : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AppointmentId { get; private set; }
    public string NoteType { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public bool IsInternal { get; private set; }

    public Appointment? Appointment { get; private set; }

    private AppointmentNote() { }

    public static AppointmentNote Create(
        Guid tenantId,
        Guid appointmentId,
        string noteType,
        string content,
        bool isInternal,
        Guid? createdByUserId)
    {
        return new AppointmentNote
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            AppointmentId   = appointmentId,
            NoteType        = noteType,
            Content         = content.Trim(),
            IsInternal      = isInternal,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = DateTime.UtcNow,
            UpdatedAtUtc    = DateTime.UtcNow
        };
    }

    public void Update(
        string noteType,
        string content,
        bool isInternal,
        Guid? updatedByUserId)
    {
        NoteType        = noteType;
        Content         = content.Trim();
        IsInternal      = isInternal;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
