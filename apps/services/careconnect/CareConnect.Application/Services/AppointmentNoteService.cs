using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class AppointmentNoteService : IAppointmentNoteService
{
    private readonly IAppointmentNoteRepository _notes;
    private readonly IAppointmentRepository _appointments;

    public AppointmentNoteService(IAppointmentNoteRepository notes, IAppointmentRepository appointments)
    {
        _notes        = notes;
        _appointments = appointments;
    }

    public async Task<List<AppointmentNoteResponse>> GetByAppointmentAsync(
        Guid tenantId,
        Guid appointmentId,
        Guid? callerOrgId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, appointmentId, ct)
            ?? throw new NotFoundException($"Appointment '{appointmentId}' was not found.");

        if (!isAdmin)
        {
            var isParticipant =
                (callerOrgId.HasValue && appointment.ReferringOrganizationId == callerOrgId) ||
                (callerOrgId.HasValue && appointment.ReceivingOrganizationId  == callerOrgId);

            if (!isParticipant)
                throw new NotFoundException($"Appointment '{appointmentId}' was not found.");
        }

        var rows = await _notes.GetByAppointmentAsync(tenantId, appointmentId, ct);

        if (!isAdmin)
        {
            rows = rows.Where(n => !n.IsInternal).ToList();
        }

        return rows.Select(ToResponse).ToList();
    }

    public async Task<AppointmentNoteResponse> CreateAsync(
        Guid tenantId,
        Guid appointmentId,
        Guid? userId,
        Guid? callerOrgId,
        CreateAppointmentNoteRequest request,
        CancellationToken ct = default)
    {
        var appointment = await _appointments.GetByIdAsync(tenantId, appointmentId, ct)
            ?? throw new NotFoundException($"Appointment '{appointmentId}' was not found.");

        if (callerOrgId.HasValue)
        {
            var isParticipant =
                appointment.ReferringOrganizationId == callerOrgId ||
                appointment.ReceivingOrganizationId  == callerOrgId;

            if (!isParticipant)
                throw new NotFoundException($"Appointment '{appointmentId}' was not found.");
        }

        ValidateNoteRequest(request.NoteType, request.Content);

        var note = AppointmentNote.Create(tenantId, appointmentId, request.NoteType, request.Content, request.IsInternal, userId);
        await _notes.AddAsync(note, ct);

        var loaded = await _notes.GetByIdAsync(tenantId, note.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<AppointmentNoteResponse> UpdateAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        UpdateAppointmentNoteRequest request,
        CancellationToken ct = default)
    {
        var note = await _notes.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Appointment note '{id}' was not found.");

        ValidateNoteRequest(request.NoteType, request.Content);

        note.Update(request.NoteType, request.Content, request.IsInternal, userId);
        await _notes.UpdateAsync(note, ct);

        var loaded = await _notes.GetByIdAsync(tenantId, id, ct);
        return ToResponse(loaded!);
    }

    private static void ValidateNoteRequest(string noteType, string content)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(noteType) || !AppointmentNoteType.IsValid(noteType))
            errors["noteType"] = new[] { $"'{noteType}' is not a valid note type. Allowed: {string.Join(", ", AppointmentNoteType.All)}." };

        if (string.IsNullOrWhiteSpace(content))
            errors["content"] = new[] { "Content is required." };
        else if (content.Length > 4000)
            errors["content"] = new[] { "Content must not exceed 4000 characters." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static AppointmentNoteResponse ToResponse(AppointmentNote n) => new()
    {
        Id              = n.Id,
        AppointmentId   = n.AppointmentId,
        NoteType        = n.NoteType,
        Content         = n.Content,
        IsInternal      = n.IsInternal,
        CreatedAtUtc    = n.CreatedAtUtc,
        CreatedByUserId = n.CreatedByUserId,
        UpdatedAtUtc    = n.UpdatedAtUtc,
        UpdatedByUserId = n.UpdatedByUserId
    };
}
