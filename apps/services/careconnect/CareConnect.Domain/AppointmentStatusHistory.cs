namespace CareConnect.Domain;

public class AppointmentStatusHistory
{
    public Guid Id { get; private set; }
    public Guid AppointmentId { get; private set; }
    public Guid TenantId { get; private set; }
    public string OldStatus { get; private set; } = string.Empty;
    public string NewStatus { get; private set; } = string.Empty;
    public Guid? ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public string? Notes { get; private set; }

    public Appointment? Appointment { get; private set; }

    private AppointmentStatusHistory() { }

    public static AppointmentStatusHistory Create(
        Guid appointmentId,
        Guid tenantId,
        string oldStatus,
        string newStatus,
        Guid? changedByUserId,
        string? notes)
    {
        return new AppointmentStatusHistory
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointmentId,
            TenantId = tenantId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = DateTime.UtcNow,
            Notes = notes?.Trim()
        };
    }
}
