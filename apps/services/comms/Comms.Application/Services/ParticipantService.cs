using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;

namespace Comms.Application.Services;

public class ParticipantService : IParticipantService
{
    private readonly IParticipantRepository _repo;
    private readonly IConversationRepository _conversationRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<ParticipantService> _logger;

    public ParticipantService(
        IParticipantRepository repo,
        IConversationRepository conversationRepo,
        IAuditPublisher audit,
        ILogger<ParticipantService> logger)
    {
        _repo = repo;
        _conversationRepo = conversationRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ParticipantResponse> AddAsync(
        Guid tenantId, Guid orgId, Guid userId, Guid conversationId,
        AddParticipantRequest request, CancellationToken ct = default)
    {
        _ = await _conversationRepo.GetByIdAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' not found.");

        var existing = await _repo.FindActiveAsync(tenantId, conversationId, request.UserId, request.ExternalEmail, ct);
        if (existing is not null)
            throw new InvalidOperationException("An active participant with the same identity already exists in this conversation.");

        var participant = ConversationParticipant.Create(
            conversationId, tenantId, orgId,
            request.ParticipantType, request.Role, request.CanReply,
            userId,
            request.UserId, request.ExternalName, request.ExternalEmail);

        await _repo.AddAsync(participant, ct);

        _logger.LogInformation("Participant {ParticipantId} added to conversation {ConversationId}",
            participant.Id, conversationId);

        _audit.Publish("ParticipantAdded", "Created", $"Participant added to conversation {conversationId}",
            tenantId, userId, "ConversationParticipant", participant.Id.ToString());

        return ToResponse(participant);
    }

    public async Task<List<ParticipantResponse>> ListByConversationAsync(
        Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        var participants = await _repo.ListByConversationAsync(tenantId, conversationId, ct);
        return participants.Select(ToResponse).ToList();
    }

    public async Task DeactivateAsync(
        Guid tenantId, Guid conversationId, Guid participantId, Guid userId,
        CancellationToken ct = default)
    {
        var participants = await _repo.ListByConversationAsync(tenantId, conversationId, ct);
        var participant = participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant '{participantId}' not found.");

        participant.Deactivate(userId);
        await _repo.UpdateAsync(participant, ct);

        _logger.LogInformation("Participant {ParticipantId} deactivated in conversation {ConversationId}",
            participantId, conversationId);

        _audit.Publish("ParticipantDeactivated", "Deactivated", $"Participant deactivated in conversation {conversationId}",
            tenantId, userId, "ConversationParticipant", participantId.ToString());
    }

    private static ParticipantResponse ToResponse(ConversationParticipant p) => new(
        p.Id, p.ConversationId,
        p.ParticipantType, p.UserId, p.ExternalName, p.ExternalEmail,
        p.Role, p.CanReply, p.IsActive, p.JoinedAtUtc, p.CreatedAtUtc);
}
