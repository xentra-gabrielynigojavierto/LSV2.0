using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;

namespace Comms.Application.Services;

public class QueueEscalationConfigService : IQueueEscalationConfigService
{
    private readonly IQueueEscalationConfigRepository _repo;
    private readonly IConversationQueueRepository _queueRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<QueueEscalationConfigService> _logger;

    public QueueEscalationConfigService(
        IQueueEscalationConfigRepository repo,
        IConversationQueueRepository queueRepo,
        IAuditPublisher audit,
        ILogger<QueueEscalationConfigService> logger)
    {
        _repo = repo;
        _queueRepo = queueRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<QueueEscalationConfigResponse> CreateOrUpdateAsync(
        Guid tenantId, Guid queueId, CreateQueueEscalationConfigRequest request, Guid userId, CancellationToken ct = default)
    {
        var queue = await _queueRepo.GetByIdAsync(tenantId, queueId, ct)
            ?? throw new KeyNotFoundException($"Queue '{queueId}' not found.");

        var existing = await _repo.GetByQueueAsync(tenantId, queueId, ct);
        if (existing is not null)
        {
            existing.Update(request.FallbackUserId, true, userId);
            await _repo.UpdateAsync(existing, ct);

            _audit.Publish("QueueEscalationConfigUpdated", "Updated",
                "Queue escalation config updated",
                tenantId, userId, "QueueEscalationConfig", existing.Id.ToString(),
                metadata: $"{{\"queueId\":\"{queueId}\",\"fallbackUserId\":\"{request.FallbackUserId}\"}}");

            return ToResponse(existing);
        }

        var config = QueueEscalationConfig.Create(tenantId, queueId, request.FallbackUserId, userId);
        await _repo.AddAsync(config, ct);

        _audit.Publish("QueueEscalationConfigCreated", "Created",
            "Queue escalation config created",
            tenantId, userId, "QueueEscalationConfig", config.Id.ToString(),
            metadata: $"{{\"queueId\":\"{queueId}\",\"fallbackUserId\":\"{request.FallbackUserId}\"}}");

        return ToResponse(config);
    }

    public async Task<QueueEscalationConfigResponse> UpdateAsync(
        Guid tenantId, Guid queueId, UpdateQueueEscalationConfigRequest request, Guid userId, CancellationToken ct = default)
    {
        var config = await _repo.GetByQueueAsync(tenantId, queueId, ct)
            ?? throw new KeyNotFoundException($"No escalation config found for queue '{queueId}'.");

        config.Update(request.FallbackUserId, request.IsActive, userId);
        await _repo.UpdateAsync(config, ct);

        _audit.Publish("QueueEscalationConfigUpdated", "Updated",
            "Queue escalation config updated",
            tenantId, userId, "QueueEscalationConfig", config.Id.ToString(),
            metadata: $"{{\"queueId\":\"{queueId}\",\"fallbackUserId\":\"{request.FallbackUserId}\",\"isActive\":{request.IsActive.ToString().ToLower()}}}");

        return ToResponse(config);
    }

    public async Task<QueueEscalationConfigResponse?> GetByQueueAsync(
        Guid tenantId, Guid queueId, CancellationToken ct = default)
    {
        var config = await _repo.GetByQueueAsync(tenantId, queueId, ct);
        return config is not null ? ToResponse(config) : null;
    }

    private static QueueEscalationConfigResponse ToResponse(QueueEscalationConfig c) => new(
        c.Id, c.TenantId, c.QueueId, c.FallbackUserId, c.IsActive, c.CreatedAtUtc, c.UpdatedAtUtc);
}
