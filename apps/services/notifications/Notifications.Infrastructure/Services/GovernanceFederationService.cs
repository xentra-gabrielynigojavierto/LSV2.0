using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

public sealed class GovernanceFederationService : IGovernanceFederationService
{
    private readonly NotificationsDbContext        _db;
    private readonly GovernanceFederationOptions   _opts;
    private readonly ILogger<GovernanceFederationService> _logger;

    public GovernanceFederationService(
        NotificationsDbContext db,
        IOptions<GovernanceFederationOptions> opts,
        ILogger<GovernanceFederationService> logger)
    {
        _db     = db;
        _opts   = opts.Value;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Channel Scope
    // -----------------------------------------------------------------------

    public async Task<ChannelScopeDto> CreateChannelScopeAsync(
        CreateChannelScopeRequest request, CancellationToken ct = default)
    {
        if (!GovernanceChannelScope.ChannelTypes.IsValid(request.ChannelType))
            throw new InvalidOperationException($"Unknown channel type: {request.ChannelType}");
        if (!GovernanceChannelScope.ChannelScopeModes.IsValid(request.ScopeMode))
            throw new InvalidOperationException($"Unknown scope mode: {request.ScopeMode}");

        var now = DateTime.UtcNow;
        var entity = new GovernanceChannelScope
        {
            Id          = Guid.NewGuid(),
            ChannelType = request.ChannelType.ToLowerInvariant(),
            ScopeMode   = request.ScopeMode,
            Enabled     = request.Enabled,
            Priority    = request.Priority,
            Description = request.Description,
            CreatedAt   = now,
            UpdatedAt   = now,
            CreatedBy   = request.RequestedBy,
            UpdatedBy   = request.RequestedBy,
        };
        _db.GovernanceChannelScopes.Add(entity);

        await WriteAuditAsync(
            entityType: GovernanceFederationAuditEvent.EntityTypes.ChannelScope,
            entityId:   entity.Id,
            eventType:  GovernanceFederationAuditEvent.EventTypes.ChannelScopeCreated,
            channelType: entity.ChannelType,
            newState:   entity.Enabled ? "enabled" : "disabled",
            actor:      request.RequestedBy,
            nowUtc:     now, ct: ct);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("GovernanceFederation: created channel scope {Id} for channel {Ch}", entity.Id, entity.ChannelType);
        return MapScope(entity);
    }

    public async Task<ChannelScopeDto> UpdateChannelScopeAsync(
        Guid scopeId, UpdateChannelScopeRequest request, CancellationToken ct = default)
    {
        var entity = await _db.GovernanceChannelScopes.FindAsync(new object[] { scopeId }, ct)
            ?? throw new InvalidOperationException($"Channel scope {scopeId} not found");

        var prevState = entity.Enabled ? "enabled" : "disabled";
        var now = DateTime.UtcNow;

        if (request.ScopeMode != null)
        {
            if (!GovernanceChannelScope.ChannelScopeModes.IsValid(request.ScopeMode))
                throw new InvalidOperationException($"Unknown scope mode: {request.ScopeMode}");
            entity.ScopeMode = request.ScopeMode;
        }
        if (request.Enabled.HasValue)  entity.Enabled     = request.Enabled.Value;
        if (request.Priority.HasValue) entity.Priority    = request.Priority.Value;
        if (request.Description != null) entity.Description = request.Description;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.RequestedBy;

        await WriteAuditAsync(
            entityType:  GovernanceFederationAuditEvent.EntityTypes.ChannelScope,
            entityId:    entity.Id,
            eventType:   GovernanceFederationAuditEvent.EventTypes.ChannelScopeUpdated,
            channelType: entity.ChannelType,
            prevState:   prevState,
            newState:    entity.Enabled ? "enabled" : "disabled",
            actor:       request.RequestedBy,
            nowUtc:      now, ct: ct);

        await _db.SaveChangesAsync(ct);
        return MapScope(entity);
    }

    public async Task<PaginatedFederationResult<ChannelScopeDto>> ListChannelScopesAsync(
        ChannelScopeQuery query, CancellationToken ct = default)
    {
        var q = _db.GovernanceChannelScopes.AsNoTracking().AsQueryable();
        if (query.ChannelType != null) q = q.Where(x => x.ChannelType == query.ChannelType);
        if (query.ScopeMode   != null) q = q.Where(x => x.ScopeMode   == query.ScopeMode);
        if (query.Enabled     != null) q = q.Where(x => x.Enabled      == query.Enabled);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.Priority).ThenBy(x => x.ChannelType)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedFederationResult<ChannelScopeDto>(total, query.Page, query.PageSize,
            items.Select(MapScope).ToList());
    }

    // -----------------------------------------------------------------------
    // Federated Rule Packs
    // -----------------------------------------------------------------------

    public async Task<FederatedRulePackDto> FederateRulePackAsync(
        FederateRulePackRequest request, CancellationToken ct = default)
    {
        if (!GovernanceChannelScope.ChannelTypes.IsValid(request.ChannelType))
            throw new InvalidOperationException($"Unknown channel type: {request.ChannelType}");

        var packExists = await _db.SmsGovernanceRulePacks.AnyAsync(p => p.Id == request.RulePackId, ct);
        if (!packExists)
            throw new InvalidOperationException($"RulePack {request.RulePackId} not found");

        var channelCount = await _db.GovernanceFederatedRulePacks
            .CountAsync(x => x.ChannelType == request.ChannelType && x.Enabled, ct);
        if (channelCount >= _opts.MaxFederatedPacksPerChannel)
            throw new InvalidOperationException($"Max federated packs per channel ({_opts.MaxFederatedPacksPerChannel}) reached for {request.ChannelType}");

        var now = DateTime.UtcNow;
        var entity = new GovernanceFederatedRulePack
        {
            Id              = Guid.NewGuid(),
            RulePackId      = request.RulePackId,
            ChannelType     = request.ChannelType.ToLowerInvariant(),
            FederationGroup = request.FederationGroup,
            TenantId        = request.TenantId,
            Enabled         = true,
            Priority        = request.Priority,
            EffectiveFrom   = request.EffectiveFrom,
            EffectiveTo     = request.EffectiveTo,
            CreatedAt       = now,
            UpdatedAt       = now,
            CreatedBy       = request.RequestedBy,
            UpdatedBy       = request.RequestedBy,
        };
        _db.GovernanceFederatedRulePacks.Add(entity);

        await WriteAuditAsync(
            entityType:  GovernanceFederationAuditEvent.EntityTypes.FederatedPack,
            entityId:    entity.Id,
            eventType:   GovernanceFederationAuditEvent.EventTypes.RulePackFederated,
            channelType: entity.ChannelType,
            fedGroup:    entity.FederationGroup,
            tenantId:    entity.TenantId,
            newState:    "enabled",
            actor:       request.RequestedBy,
            nowUtc:      now, ct: ct);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("GovernanceFederation: federated pack {PackId} to channel {Ch}", entity.RulePackId, entity.ChannelType);
        return MapFederatedPack(entity);
    }

    public async Task<FederationOperationResult> DisableFederatedRulePackAsync(
        Guid mappingId, string requestedBy, string? reason, CancellationToken ct = default)
    {
        var entity = await _db.GovernanceFederatedRulePacks.FindAsync(new object[] { mappingId }, ct);
        if (entity == null) return new FederationOperationResult(false, "Mapping not found");

        var now = DateTime.UtcNow;
        entity.Enabled   = false;
        entity.UpdatedAt = now;
        entity.UpdatedBy = requestedBy;

        await WriteAuditAsync(
            entityType: GovernanceFederationAuditEvent.EntityTypes.FederatedPack,
            entityId:   entity.Id,
            eventType:  GovernanceFederationAuditEvent.EventTypes.RulePackUnfederated,
            channelType: entity.ChannelType,
            prevState:  "enabled", newState: "disabled",
            actor:      requestedBy, reason: reason,
            nowUtc:     now, ct: ct);

        await _db.SaveChangesAsync(ct);
        return new FederationOperationResult(true);
    }

    public async Task<PaginatedFederationResult<FederatedRulePackDto>> ListFederatedRulePacksAsync(
        FederatedRulePackQuery query, CancellationToken ct = default)
    {
        var q = _db.GovernanceFederatedRulePacks.AsNoTracking().AsQueryable();
        if (query.ChannelType     != null) q = q.Where(x => x.ChannelType     == query.ChannelType);
        if (query.RulePackId      != null) q = q.Where(x => x.RulePackId      == query.RulePackId);
        if (query.TenantId        != null) q = q.Where(x => x.TenantId        == query.TenantId);
        if (query.FederationGroup != null) q = q.Where(x => x.FederationGroup == query.FederationGroup);
        if (query.Enabled         != null) q = q.Where(x => x.Enabled         == query.Enabled);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.ChannelType).ThenBy(x => x.Priority)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedFederationResult<FederatedRulePackDto>(total, query.Page, query.PageSize,
            items.Select(MapFederatedPack).ToList());
    }

    // -----------------------------------------------------------------------
    // Federation Overlays
    // -----------------------------------------------------------------------

    public async Task<FederationOverlayDto> CreateFederationOverlayAsync(
        CreateFederationOverlayRequest request, CancellationToken ct = default)
    {
        if (!GovernanceChannelScope.ChannelTypes.IsValid(request.ChannelType))
            throw new InvalidOperationException($"Unknown channel type: {request.ChannelType}");
        if (!GovernanceFederationOverlay.OverlayTypes.IsValid(request.OverlayType))
            throw new InvalidOperationException($"Unknown overlay type: {request.OverlayType}");
        if (GovernanceFederationOverlay.HasSensitiveContent(request.OverlayJson))
            throw new InvalidOperationException("OverlayJson contains disallowed content");
        if (request.OverlayJson?.Length > 4000)
            throw new InvalidOperationException("OverlayJson exceeds 4000 character limit");

        if (_opts.EnableCrossChannelOverlays)
        {
            var overlayCount = await _db.GovernanceFederationOverlays
                .CountAsync(x => x.ChannelType == request.ChannelType && x.Enabled, ct);
            if (overlayCount >= _opts.MaxFederationOverlaysPerChannel)
                throw new InvalidOperationException($"Max federation overlays per channel ({_opts.MaxFederationOverlaysPerChannel}) reached for {request.ChannelType}");
        }

        var now = DateTime.UtcNow;
        var entity = new GovernanceFederationOverlay
        {
            Id           = Guid.NewGuid(),
            TenantId     = request.TenantId,
            ChannelType  = request.ChannelType.ToLowerInvariant(),
            RulePackId   = request.RulePackId,
            RuleId       = request.RuleId,
            OverlayType  = request.OverlayType,
            OverlayState = GovernanceFederationOverlay.OverlayStates.Draft,
            OverlayJson  = request.OverlayJson,
            Priority     = request.Priority,
            Enabled      = true,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo   = request.EffectiveTo,
            CreatedAt    = now,
            UpdatedAt    = now,
            CreatedBy    = request.RequestedBy,
            UpdatedBy    = request.RequestedBy,
        };
        _db.GovernanceFederationOverlays.Add(entity);

        await WriteAuditAsync(
            entityType:  GovernanceFederationAuditEvent.EntityTypes.FederationOverlay,
            entityId:    entity.Id,
            eventType:   GovernanceFederationAuditEvent.EventTypes.OverlayCreated,
            channelType: entity.ChannelType,
            tenantId:    entity.TenantId,
            newState:    GovernanceFederationOverlay.OverlayStates.Draft,
            actor:       request.RequestedBy,
            nowUtc:      now, ct: ct);

        await _db.SaveChangesAsync(ct);
        return MapOverlay(entity);
    }

    public async Task<FederationOperationResult> ActivateFederationOverlayAsync(
        Guid overlayId, string requestedBy, CancellationToken ct = default)
    {
        var entity = await _db.GovernanceFederationOverlays.FindAsync(new object[] { overlayId }, ct);
        if (entity == null) return new FederationOperationResult(false, "Overlay not found");
        if (GovernanceFederationOverlay.OverlayStates.Terminal.Contains(entity.OverlayState))
            return new FederationOperationResult(false, $"Overlay in terminal state: {entity.OverlayState}");

        var now = DateTime.UtcNow;
        var prev = entity.OverlayState;
        entity.OverlayState = GovernanceFederationOverlay.OverlayStates.Active;
        entity.UpdatedAt    = now;
        entity.UpdatedBy    = requestedBy;

        await WriteAuditAsync(
            entityType: GovernanceFederationAuditEvent.EntityTypes.FederationOverlay,
            entityId:   entity.Id,
            eventType:  GovernanceFederationAuditEvent.EventTypes.OverlayActivated,
            channelType: entity.ChannelType,
            tenantId:   entity.TenantId,
            prevState:  prev,
            newState:   GovernanceFederationOverlay.OverlayStates.Active,
            actor:      requestedBy, nowUtc: now, ct: ct);

        await _db.SaveChangesAsync(ct);
        return new FederationOperationResult(true);
    }

    public async Task<FederationOperationResult> DisableFederationOverlayAsync(
        Guid overlayId, string requestedBy, string? reason, CancellationToken ct = default)
    {
        var entity = await _db.GovernanceFederationOverlays.FindAsync(new object[] { overlayId }, ct);
        if (entity == null) return new FederationOperationResult(false, "Overlay not found");

        var now = DateTime.UtcNow;
        var prev = entity.OverlayState;
        entity.OverlayState = GovernanceFederationOverlay.OverlayStates.Inactive;
        entity.Enabled      = false;
        entity.UpdatedAt    = now;
        entity.UpdatedBy    = requestedBy;

        await WriteAuditAsync(
            entityType: GovernanceFederationAuditEvent.EntityTypes.FederationOverlay,
            entityId:   entity.Id,
            eventType:  GovernanceFederationAuditEvent.EventTypes.OverlayDisabled,
            channelType: entity.ChannelType,
            tenantId:   entity.TenantId,
            prevState:  prev,
            newState:   GovernanceFederationOverlay.OverlayStates.Inactive,
            actor:      requestedBy, reason: reason,
            nowUtc:     now, ct: ct);

        await _db.SaveChangesAsync(ct);
        return new FederationOperationResult(true);
    }

    public async Task<PaginatedFederationResult<FederationOverlayDto>> ListFederationOverlaysAsync(
        FederationOverlayQuery query, CancellationToken ct = default)
    {
        var q = _db.GovernanceFederationOverlays.AsNoTracking().AsQueryable();
        if (query.ChannelType  != null) q = q.Where(x => x.ChannelType  == query.ChannelType);
        if (query.TenantId     != null) q = q.Where(x => x.TenantId     == query.TenantId);
        if (query.RulePackId   != null) q = q.Where(x => x.RulePackId   == query.RulePackId);
        if (query.OverlayType  != null) q = q.Where(x => x.OverlayType  == query.OverlayType);
        if (query.OverlayState != null) q = q.Where(x => x.OverlayState == query.OverlayState);
        if (query.Enabled      != null) q = q.Where(x => x.Enabled      == query.Enabled);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.ChannelType).ThenBy(x => x.Priority)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(ct);

        return new PaginatedFederationResult<FederationOverlayDto>(total, query.Page, query.PageSize,
            items.Select(MapOverlay).ToList());
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private async Task WriteAuditAsync(
        string entityType, Guid entityId, string eventType,
        string? channelType = null, string? fedGroup = null, Guid? tenantId = null,
        string? prevState = null, string? newState = null,
        string? actor = null, string? reason = null,
        DateTime nowUtc = default, CancellationToken ct = default)
    {
        _db.GovernanceFederationAuditEvents.Add(new GovernanceFederationAuditEvent
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            ChannelType    = channelType,
            FederationGroup = fedGroup,
            EntityType     = entityType,
            EntityId       = entityId,
            EventType      = eventType,
            PreviousState  = prevState,
            NewState       = newState,
            Actor          = actor,
            Reason         = reason,
            CreatedAt      = nowUtc == default ? DateTime.UtcNow : nowUtc,
        });
        await Task.CompletedTask;
    }

    private static ChannelScopeDto MapScope(GovernanceChannelScope e) => new(
        e.Id, e.ChannelType, e.ScopeMode, e.Enabled, e.Priority,
        e.Description, e.CreatedAt, e.UpdatedAt, e.CreatedBy);

    private static FederatedRulePackDto MapFederatedPack(GovernanceFederatedRulePack e) => new(
        e.Id, e.RulePackId, e.ChannelType, e.FederationGroup, e.TenantId,
        e.Enabled, e.Priority, e.EffectiveFrom, e.EffectiveTo, e.CreatedAt, e.CreatedBy);

    private static FederationOverlayDto MapOverlay(GovernanceFederationOverlay e) => new(
        e.Id, e.TenantId, e.ChannelType, e.RulePackId, e.RuleId,
        e.OverlayType, e.OverlayState, e.Priority, e.Enabled,
        e.EffectiveFrom, e.EffectiveTo, e.CreatedAt, e.CreatedBy);
}
