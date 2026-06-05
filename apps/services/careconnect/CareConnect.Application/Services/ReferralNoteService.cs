using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Application.Services;

public class ReferralNoteService : IReferralNoteService
{
    private readonly IReferralNoteRepository _notes;
    private readonly IReferralRepository _referrals;

    public ReferralNoteService(IReferralNoteRepository notes, IReferralRepository referrals)
    {
        _notes    = notes;
        _referrals = referrals;
    }

    public async Task<List<ReferralNoteResponse>> GetByReferralAsync(
        Guid tenantId,
        Guid referralId,
        Guid? callerOrgId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var referral = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        if (!isAdmin)
        {
            var isParticipant =
                (callerOrgId.HasValue && referral.ReferringOrganizationId == callerOrgId) ||
                (callerOrgId.HasValue && referral.ReceivingOrganizationId  == callerOrgId);

            if (!isParticipant)
                throw new NotFoundException($"Referral '{referralId}' was not found.");
        }

        var rows = await _notes.GetByReferralAsync(tenantId, referralId, ct);

        if (!isAdmin)
        {
            rows = rows.Where(n =>
                n.VisibilityScope != "INTERNAL" ||
                (callerOrgId.HasValue && n.OwnerOrganizationId == callerOrgId))
                .ToList();
        }

        return rows.Select(ToResponse).ToList();
    }

    public async Task<ReferralNoteResponse> CreateAsync(
        Guid tenantId,
        Guid referralId,
        Guid? userId,
        Guid? callerOrgId,
        CreateReferralNoteRequest request,
        CancellationToken ct = default)
    {
        _ = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        ValidateNoteRequest(request.NoteType, request.Content);

        var visibilityScope = request.IsInternal ? "INTERNAL" : "SHARED";
        var note = ReferralNote.Create(tenantId, referralId, ownerOrganizationId: callerOrgId, visibilityScope, request.NoteType, request.Content, userId);
        await _notes.AddAsync(note, ct);

        var loaded = await _notes.GetByIdAsync(tenantId, note.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<ReferralNoteResponse> UpdateAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        UpdateReferralNoteRequest request,
        CancellationToken ct = default)
    {
        var note = await _notes.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Referral note '{id}' was not found.");

        ValidateNoteRequest(request.NoteType, request.Content);

        var visibilityScope = request.IsInternal ? "INTERNAL" : "SHARED";
        note.Update(request.NoteType, request.Content, visibilityScope, userId);
        await _notes.UpdateAsync(note, ct);

        var loaded = await _notes.GetByIdAsync(tenantId, id, ct);
        return ToResponse(loaded!);
    }

    private static void ValidateNoteRequest(string noteType, string content)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(noteType) || !ReferralNoteType.IsValid(noteType))
            errors["noteType"] = new[] { $"'{noteType}' is not a valid note type. Allowed: {string.Join(", ", ReferralNoteType.All)}." };

        if (string.IsNullOrWhiteSpace(content))
            errors["content"] = new[] { "Content is required." };
        else if (content.Length > 4000)
            errors["content"] = new[] { "Content must not exceed 4000 characters." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static ReferralNoteResponse ToResponse(ReferralNote n) => new()
    {
        Id              = n.Id,
        ReferralId      = n.ReferralId,
        NoteType        = n.NoteType,
        Content         = n.Content,
        IsInternal      = n.IsInternal,
        CreatedAtUtc    = n.CreatedAtUtc,
        CreatedByUserId = n.CreatedByUserId,
        UpdatedAtUtc    = n.UpdatedAtUtc,
        UpdatedByUserId = n.UpdatedByUserId
    };
}
