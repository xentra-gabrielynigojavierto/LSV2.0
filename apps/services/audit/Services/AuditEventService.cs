using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Models;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Utilities;

namespace PlatformAuditEventService.Services;

public sealed class AuditEventService : IAuditEventService
{
    private readonly IAuditEventRepository      _repository;
    private readonly byte[]                     _hmacSecret;
    private readonly IntegrityOptions           _integrity;
    private readonly ILogger<AuditEventService> _logger;

    public AuditEventService(
        IAuditEventRepository        repository,
        IOptions<IntegrityOptions>   integrityOptions,
        ILogger<AuditEventService>   logger)
    {
        _repository = repository;
        _integrity  = integrityOptions.Value;
        _logger     = logger;
        _hmacSecret = Convert.FromBase64String(
            _integrity.HmacKeyBase64
            ?? throw new InvalidOperationException(
                "Integrity:HmacKeyBase64 is required. " +
                "Set it via appsettings or environment variable Integrity__HmacKeyBase64. " +
                "Generate a key with: openssl rand -base64 32"));
    }

    public async Task<AuditEventResponse> IngestAsync(IngestAuditEventRequest request, CancellationToken ct = default)
    {
        var model     = AuditEventMapper.ToModel(request, _hmacSecret);
        var persisted = await _repository.AppendAsync(model, ct);

        _logger.LogInformation(
            "AuditEvent ingested: Id={Id} Source={Source} EventType={EventType} TenantId={TenantId} Outcome={Outcome}",
            persisted.Id, persisted.Source, persisted.EventType, persisted.TenantId, persisted.Outcome);

        return ToResponse(persisted);
    }

    public async Task<AuditEventResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var evt = await _repository.GetByIdAsync(id, ct);
        return evt is null ? null : ToResponse(evt);
    }

    public async Task<PagedResult<AuditEventResponse>> QueryAsync(AuditEventQueryRequest query, CancellationToken ct = default)
    {
        var result = await _repository.QueryAsync(query, ct);
        return new PagedResult<AuditEventResponse>
        {
            Items      = result.Items.Select(ToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize,
        };
    }

    public Task<long> CountAsync(CancellationToken ct = default) =>
        _repository.CountAsync(ct);

    // ── Private helpers ───────────────────────────────────────────────────────

    private AuditEventResponse ToResponse(AuditEvent evt)
    {
        if (_integrity.VerifyOnRead && evt.IntegrityHash is not null)
        {
            var valid = IntegrityHasher.Verify(evt, _hmacSecret);
            if (!valid)
            {
                _logger.LogCritical(
                    "INTEGRITY VIOLATION: AuditEvent Id={Id} Source={Source} EventType={EventType} " +
                    "failed HMAC-SHA256 verification. Record may have been tampered with.",
                    evt.Id, evt.Source, evt.EventType);
            }
        }

        return AuditEventMapper.ToResponse(evt);
    }
}
