using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using BuildingBlocks.FlowClient;
using Flow.Application.DTOs;
using Flow.Application.Engines.WorkflowEngine;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flow.Api.Controllers.V1;

/// <summary>
/// LS-FLOW-HARDEN-A1 — atomic ownership-aware execution surface for
/// product-driven workflows. Replaces the Phase-5 pattern where the
/// product service called Flow twice (list-mappings to verify ownership
/// → execution endpoint), which had a TOCTOU window.
///
/// <para>
/// Every request resolves the workflow instance through a SINGLE EF
/// query that joins <see cref="ProductWorkflowMapping"/> against the
/// instance, filtered by:
/// <list type="bullet">
///   <item>Tenant — implicit, via the <c>FlowDbContext</c> tenant query filter.</item>
///   <item>Product key — derived from the <c>{product}</c> route slug.</item>
///   <item>Source entity — exact match on (<c>{sourceEntityType}</c>, <c>{sourceEntityId}</c>).</item>
///   <item>Workflow instance id — must be the mapping's <c>WorkflowInstanceId</c>.</item>
/// </list>
/// Any mismatch is surfaced uniformly as 404
/// <see cref="FlowErrorCodes.WorkflowInstanceNotOwned"/> — no
/// information disclosure as to whether the row exists under a
/// different parent.
/// </para>
///
/// <para>
/// Capability policy gating is applied for end-user callers only. When
/// the caller is a service token (machine-to-machine, originating from
/// a product service that already gated the user), only ownership +
/// tenant + product correlation is enforced — service tokens cannot
/// satisfy per-permission capability claims, and re-applying them here
/// would block the legitimate "service-on-behalf-of-user" pattern.
/// Tenant scoping is non-bypassable (the query filter enforces it for
/// both caller types).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/product-workflows")]
[Authorize(Policy = Policies.AuthenticatedUser)]
public sealed class ProductWorkflowExecutionController : ControllerBase
{
    private readonly IFlowDbContext _db;
    private readonly IWorkflowEngine _engine;
    private readonly IAuthorizationService _authz;
    private readonly ICallerContextAccessor _caller;
    private readonly ILogger<ProductWorkflowExecutionController> _logger;

    public ProductWorkflowExecutionController(
        IFlowDbContext db,
        IWorkflowEngine engine,
        IAuthorizationService authz,
        ICallerContextAccessor caller,
        ILogger<ProductWorkflowExecutionController> logger)
    {
        _db      = db;
        _engine  = engine;
        _authz   = authz;
        _caller  = caller;
        _logger  = logger;
    }

    [HttpGet("{product}/{sourceEntityType}/{sourceEntityId}/{workflowInstanceId:guid}")]
    public async Task<IActionResult> Get(
        string product, string sourceEntityType, string sourceEntityId, Guid workflowInstanceId,
        CancellationToken ct)
    {
        using var scope = BeginScope(product, sourceEntityType, sourceEntityId, workflowInstanceId);

        var (instance, failure) = await ResolveOwnedAsync(product, sourceEntityType, sourceEntityId, workflowInstanceId, ct);
        if (failure is not null) return failure;

        return Ok(WorkflowEngine.Map(instance!));
    }

    [HttpPost("{product}/{sourceEntityType}/{sourceEntityId}/{workflowInstanceId:guid}/advance")]
    public async Task<IActionResult> Advance(
        string product, string sourceEntityType, string sourceEntityId, Guid workflowInstanceId,
        [FromBody] AdvanceWorkflowRequest body,
        CancellationToken ct)
    {
        using var scope = BeginScope(product, sourceEntityType, sourceEntityId, workflowInstanceId);

        if (body is null || string.IsNullOrWhiteSpace(body.ExpectedCurrentStepKey))
            return BadRequest(new { error = "expectedCurrentStepKey is required." });

        var (instance, failure) = await ResolveOwnedAsync(product, sourceEntityType, sourceEntityId, workflowInstanceId, ct);
        if (failure is not null) return failure;

        try
        {
            var result = await _engine.AdvanceAsync(instance!.Id, body.ExpectedCurrentStepKey, body.ToStepKey, ct);
            return Ok(result);
        }
        catch (NotFoundException) { return NotOwned(); }
        catch (ValidationException vex) { return BadRequest(new { error = vex.Message, errors = vex.Errors }); }
        catch (InvalidWorkflowTransitionException ex)
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
    }

    [HttpPost("{product}/{sourceEntityType}/{sourceEntityId}/{workflowInstanceId:guid}/complete")]
    public async Task<IActionResult> Complete(
        string product, string sourceEntityType, string sourceEntityId, Guid workflowInstanceId,
        CancellationToken ct)
    {
        using var scope = BeginScope(product, sourceEntityType, sourceEntityId, workflowInstanceId);

        var (instance, failure) = await ResolveOwnedAsync(product, sourceEntityType, sourceEntityId, workflowInstanceId, ct);
        if (failure is not null) return failure;

        try
        {
            var result = await _engine.CompleteAsync(instance!.Id, ct);
            return Ok(result);
        }
        catch (NotFoundException) { return NotOwned(); }
        catch (InvalidWorkflowTransitionException ex)
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
    }

    // ------------------------------------------------------------------
    // Resolution + helpers
    // ------------------------------------------------------------------

    private async Task<(WorkflowInstance? Instance, IActionResult? Failure)> ResolveOwnedAsync(
        string productSlug, string sourceEntityType, string sourceEntityId, Guid workflowInstanceId,
        CancellationToken ct)
    {
        var productKey = ProductKeyFromSlug(productSlug);
        if (productKey is null)
        {
            _logger.LogWarning("ProductWorkflowExecution: unknown product slug {Slug}", productSlug);
            return (null, NotOwned());
        }

        // Capability gate (user callers only — service tokens are gated by
        // the originating product service before they reach Flow).
        var caller = _caller.Current;
        if (caller.Type == CallerType.User)
        {
            var policy = CapabilityPolicyFor(productKey);
            var authz = await _authz.AuthorizeAsync(User, policy);
            if (!authz.Succeeded)
            {
                _logger.LogInformation(
                    "ProductWorkflowExecution: capability denied policy={Policy} caller={CallerType}",
                    policy, caller.Type);
                return (null, Forbid());
            }
        }

        // Single atomic read: mapping joined to instance, both tenant-filtered
        // by FlowDbContext's query filter. Mismatch on any axis → 404.
        var instance = await (
            from m in _db.ProductWorkflowMappings.AsNoTracking()
            join i in _db.WorkflowInstances.AsNoTracking() on m.WorkflowInstanceId equals i.Id
            where m.WorkflowInstanceId == workflowInstanceId
               && m.ProductKey         == productKey
               && m.SourceEntityType   == sourceEntityType
               && m.SourceEntityId     == sourceEntityId
            select i
        ).FirstOrDefaultAsync(ct);

        if (instance is null)
        {
            _logger.LogInformation(
                "ProductWorkflowExecution: ownership denied product={ProductKey} entity={EntityType}/{EntityId} instance={InstanceId} caller={CallerType}",
                productKey, sourceEntityType, sourceEntityId, workflowInstanceId, caller.Type);
            return (null, NotOwned());
        }

        return (instance, null);
    }

    private IActionResult NotOwned() => NotFound(new
    {
        error = "Workflow instance is not associated with this resource.",
        code  = FlowErrorCodes.WorkflowInstanceNotOwned
    });

    private IDisposable? BeginScope(string product, string sourceEntityType, string sourceEntityId, Guid workflowInstanceId)
    {
        var c = _caller.Current;
        return _logger.BeginScope(
            "exec product={Product} entity={EntityType}/{EntityId} instance={InstanceId} caller={CallerType} tenant={TenantId} sub={Subject} actor={Actor}",
            product, sourceEntityType, sourceEntityId, workflowInstanceId,
            c.Type, c.TenantId, c.Subject, c.Actor);
    }

    private static string? ProductKeyFromSlug(string slug) => slug?.ToLowerInvariant() switch
    {
        "synqlien"   => ProductKeys.SynqLiens,
        "careconnect"=> ProductKeys.CareConnect,
        "synqfund"   => ProductKeys.SynqFund,
        _ => null
    };

    private static string CapabilityPolicyFor(string productKey) => productKey switch
    {
        ProductKeys.SynqLiens   => Policies.CanSellLien,
        ProductKeys.CareConnect => Policies.CanReferCareConnect,
        ProductKeys.SynqFund    => Policies.CanReferFund,
        _ => Policies.AuthenticatedUser
    };
}
