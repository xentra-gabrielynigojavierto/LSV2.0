using System.Text.Json;
using Documents.Domain.Entities;
using Documents.Domain.Interfaces;
using Documents.Application.Models;
using Microsoft.Extensions.Logging;

namespace Documents.Application.Services;

public sealed class AuditService
{
    private readonly IAuditRepository _repo;
    private readonly ILogger<AuditService> _log;

    public AuditService(IAuditRepository repo, ILogger<AuditService> log)
    {
        _repo = repo;
        _log  = log;
    }

    public async Task LogAsync(
        string         eventName,
        RequestContext ctx,
        Guid?          documentId = null,
        string         outcome    = "SUCCESS",
        object?        detail     = null)
    {
        try
        {
            var audit = new DocumentAudit
            {
                Id            = Guid.NewGuid(),
                TenantId      = ctx.Principal.TenantId,
                DocumentId    = documentId,
                Event         = eventName,
                ActorId       = ctx.Principal.UserId,
                ActorEmail    = ctx.Principal.Email,
                Outcome       = outcome,
                IpAddress     = ctx.IpAddress,
                UserAgent     = ctx.UserAgent,
                CorrelationId = ctx.CorrelationId,
                Detail        = detail is null ? null : JsonSerializer.Serialize(detail),
                OccurredAt    = DateTime.UtcNow,
            };

            await _repo.InsertAsync(audit);
        }
        catch (Exception ex)
        {
            // Audit failures are non-fatal — log and continue
            _log.LogError(ex, "Audit insert failed for event {Event} on document {DocId}", eventName, documentId);
        }
    }
}
