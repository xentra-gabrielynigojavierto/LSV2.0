namespace CareConnect.Application.DTOs;

public class CreateReferralNoteRequest
{
    public string NoteType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public class UpdateReferralNoteRequest
{
    public string NoteType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public class ReferralNoteResponse
{
    public Guid Id { get; init; }
    public Guid ReferralId { get; init; }
    public string NoteType { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public Guid? UpdatedByUserId { get; init; }
}

public class CreateAppointmentNoteRequest
{
    public string NoteType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public class UpdateAppointmentNoteRequest
{
    public string NoteType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public class AppointmentNoteResponse
{
    public Guid Id { get; init; }
    public Guid AppointmentId { get; init; }
    public string NoteType { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public Guid? UpdatedByUserId { get; init; }
}
