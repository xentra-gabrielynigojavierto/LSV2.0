using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;

namespace Comms.Application.Services;

public class QueueService : IQueueService
{
    private readonly IConversationQueueRepository _queueRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<QueueService> _logger;

    public QueueService(
        IConversationQueueRepository queueRepo,
        IAuditPublisher audit,
        ILogger<QueueService> logger)
    {
        _queueRepo = queueRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ConversationQueueResponse> CreateAsync(
        Guid tenantId, Guid userId, CreateConversationQueueRequest request, CancellationToken ct = default)
    {
        var normalizedCode = ConversationQueue.NormalizeCode(request.Code);

        var existing = await _queueRepo.GetByCodeAsync(tenantId, normalizedCode, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Queue with code '{normalizedCode}' already exists for this tenant.");

        if (request.IsDefault)
        {
            var currentDefault = await _queueRepo.GetDefaultAsync(tenantId, ct);
            if (currentDefault is not null)
            {
                currentDefault.SetDefault(false, userId);
                await _queueRepo.UpdateAsync(currentDefault, ct);
            }
        }

        var queue = ConversationQueue.Create(tenantId, request.Name, request.Code, request.Description, request.IsDefault, userId);
        await _queueRepo.AddAsync(queue, ct);

        _logger.LogInformation("Queue {QueueId} created: {QueueCode} for tenant {TenantId}", queue.Id, queue.Code, tenantId);

        _audit.Publish("QueueCreated", "Created", $"Queue created: {queue.Name} ({queue.Code})",
            tenantId, userId, "ConversationQueue", queue.Id.ToString(),
            metadata: $"{{\"code\":\"{queue.Code}\",\"isDefault\":{queue.IsDefault.ToString().ToLower()}}}");

        return ToResponse(queue);
    }

    public async Task<ConversationQueueResponse> UpdateAsync(
        Guid tenantId, Guid queueId, Guid userId, UpdateConversationQueueRequest request, CancellationToken ct = default)
    {
        var queue = await _queueRepo.GetByIdAsync(tenantId, queueId, ct)
            ?? throw new KeyNotFoundException($"Queue '{queueId}' not found.");

        var oldName = queue.Name;
        var oldActive = queue.IsActive;

        queue.Update(request.Name, request.Description, request.IsActive, userId);
        await _queueRepo.UpdateAsync(queue, ct);

        _logger.LogInformation("Queue {QueueId} updated for tenant {TenantId}", queue.Id, tenantId);

        _audit.Publish("QueueUpdated", "Updated", $"Queue updated: {queue.Name}",
            tenantId, userId, "ConversationQueue", queue.Id.ToString(),
            metadata: $"{{\"oldName\":\"{oldName}\",\"newName\":\"{queue.Name}\",\"oldIsActive\":{oldActive.ToString().ToLower()},\"newIsActive\":{queue.IsActive.ToString().ToLower()}}}");

        return ToResponse(queue);
    }

    public async Task<ConversationQueueResponse?> GetByIdAsync(Guid tenantId, Guid queueId, CancellationToken ct = default)
    {
        var queue = await _queueRepo.GetByIdAsync(tenantId, queueId, ct);
        return queue is null ? null : ToResponse(queue);
    }

    public async Task<List<ConversationQueueResponse>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var queues = await _queueRepo.ListByTenantAsync(tenantId, ct);
        return queues.Select(ToResponse).ToList();
    }

    private static ConversationQueueResponse ToResponse(ConversationQueue q) => new(
        q.Id, q.TenantId, q.Name, q.Code, q.Description,
        q.IsDefault, q.IsActive,
        q.CreatedAtUtc, q.UpdatedAtUtc, q.CreatedByUserId);
}
