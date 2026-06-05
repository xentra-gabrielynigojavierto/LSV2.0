using BuildingBlocks.Authorization;
using Flow.Application.DTOs;
using Flow.Application.Services;
using Flow.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers.V1;

/// <summary>
/// LS-FLOW-MERGE-P3 — product-facing entry point for Flow consumption.
/// Each product gets its own route segment with its own product-capability
/// policy attached, so a CareConnect-only user cannot start SynqFund flows
/// even though they share the underlying Flow service.
///
/// Route shape: <c>/api/v1/product-workflows/{product}</c>
/// where <c>{product}</c> is one of: <c>synqlien</c>, <c>careconnect</c>, <c>synqfund</c>.
/// </summary>
[ApiController]
[Route("api/v1/product-workflows")]
[Authorize(Policy = Policies.AuthenticatedUser)]
public class ProductWorkflowsController : ControllerBase
{
    private readonly IProductWorkflowService _service;

    public ProductWorkflowsController(IProductWorkflowService service)
    {
        _service = service;
    }

    // ---------------- SynqLien ----------------
    [HttpPost("synqlien")]
    [Authorize(Policy = Policies.CanSellLien)]
    public Task<IActionResult> CreateLien([FromBody] CreateProductWorkflowRequest request, CancellationToken ct) =>
        WrapCreate(ProductKeys.SynqLiens, request, ct);

    [HttpGet("synqlien")]
    [Authorize(Policy = Policies.CanSellLien)]
    public Task<IActionResult> ListLien([FromQuery] string? sourceEntityType, [FromQuery] string? sourceEntityId, CancellationToken ct) =>
        WrapList(ProductKeys.SynqLiens, sourceEntityType, sourceEntityId, ct);

    [HttpGet("synqlien/{id:guid}")]
    [Authorize(Policy = Policies.CanSellLien)]
    public Task<IActionResult> GetLien(Guid id, CancellationToken ct) =>
        WrapGet(ProductKeys.SynqLiens, id, ct);

    // ---------------- CareConnect ----------------
    [HttpPost("careconnect")]
    [Authorize(Policy = Policies.CanReferCareConnect)]
    public Task<IActionResult> CreateCareConnect([FromBody] CreateProductWorkflowRequest request, CancellationToken ct) =>
        WrapCreate(ProductKeys.CareConnect, request, ct);

    [HttpGet("careconnect")]
    [Authorize(Policy = Policies.CanReferCareConnect)]
    public Task<IActionResult> ListCareConnect([FromQuery] string? sourceEntityType, [FromQuery] string? sourceEntityId, CancellationToken ct) =>
        WrapList(ProductKeys.CareConnect, sourceEntityType, sourceEntityId, ct);

    [HttpGet("careconnect/{id:guid}")]
    [Authorize(Policy = Policies.CanReferCareConnect)]
    public Task<IActionResult> GetCareConnect(Guid id, CancellationToken ct) =>
        WrapGet(ProductKeys.CareConnect, id, ct);

    // ---------------- SynqFund ----------------
    [HttpPost("synqfund")]
    [Authorize(Policy = Policies.CanReferFund)]
    public Task<IActionResult> CreateFund([FromBody] CreateProductWorkflowRequest request, CancellationToken ct) =>
        WrapCreate(ProductKeys.SynqFund, request, ct);

    [HttpGet("synqfund")]
    [Authorize(Policy = Policies.CanReferFund)]
    public Task<IActionResult> ListFund([FromQuery] string? sourceEntityType, [FromQuery] string? sourceEntityId, CancellationToken ct) =>
        WrapList(ProductKeys.SynqFund, sourceEntityType, sourceEntityId, ct);

    [HttpGet("synqfund/{id:guid}")]
    [Authorize(Policy = Policies.CanReferFund)]
    public Task<IActionResult> GetFund(Guid id, CancellationToken ct) =>
        WrapGet(ProductKeys.SynqFund, id, ct);

    // ---------------- helpers ----------------
    private async Task<IActionResult> WrapCreate(string productKey, CreateProductWorkflowRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(productKey, request, ct);
        // Use a literal Location header — there is no single shared GET-by-id
        // route across products (each product has its own gated GET action),
        // so we cannot use CreatedAtAction without dispatching by product.
        var slug = ProductSlugFor(productKey);
        return Created($"/api/v1/product-workflows/{slug}/{result.Id}", result);
    }

    private async Task<IActionResult> WrapList(string productKey, string? sourceEntityType, string? sourceEntityId, CancellationToken ct)
    {
        var result = !string.IsNullOrWhiteSpace(sourceEntityType) && !string.IsNullOrWhiteSpace(sourceEntityId)
            ? await _service.ListByProductEntityAsync(productKey, sourceEntityType!, sourceEntityId!, ct)
            : await _service.ListByProductAsync(productKey, ct);
        return Ok(result);
    }

    private async Task<IActionResult> WrapGet(string productKey, Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(productKey, id, ct);
        return Ok(result);
    }

    private static string ProductSlugFor(string productKey) => productKey switch
    {
        ProductKeys.SynqLiens => "synqlien",
        ProductKeys.CareConnect => "careconnect",
        ProductKeys.SynqFund => "synqfund",
        _ => "unknown"
    };
}
