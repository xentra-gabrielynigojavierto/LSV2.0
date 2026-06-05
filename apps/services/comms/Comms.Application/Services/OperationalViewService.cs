using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;

namespace Comms.Application.Services;

public class OperationalViewService : IOperationalViewService
{
    private const int MaxPageSize = 200;
    private const int DefaultPageSize = 50;
    private const int MinPage = 1;

    private static readonly HashSet<string> ValidSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "lastActivityAtUtc", "firstResponseDueAtUtc", "resolutionDueAtUtc", "priority", "createdAtUtc"
    };

    private static readonly HashSet<string> ValidSortDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc", "desc"
    };

    private readonly IOperationalConversationQueryRepository _queryRepo;
    private readonly ILogger<OperationalViewService> _logger;

    public OperationalViewService(
        IOperationalConversationQueryRepository queryRepo,
        ILogger<OperationalViewService> logger)
    {
        _queryRepo = queryRepo;
        _logger = logger;
    }

    public async Task<OperationalQueryResponse> QueryConversationsAsync(
        Guid tenantId,
        Guid userId,
        OperationalQueryRequest request,
        CancellationToken ct = default)
    {
        var normalizedRequest = NormalizeRequest(request);

        var (items, totalCount) = await _queryRepo.QueryAsync(tenantId, normalizedRequest, userId, ct);

        var hasMore = (normalizedRequest.Page * normalizedRequest.PageSize) < totalCount;

        return new OperationalQueryResponse(
            items,
            totalCount,
            normalizedRequest.Page,
            normalizedRequest.PageSize,
            hasMore);
    }

    private static OperationalQueryRequest NormalizeRequest(OperationalQueryRequest request)
    {
        var page = Math.Max(MinPage, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var sortBy = ValidSortFields.Contains(request.SortBy ?? "") ? request.SortBy! : "lastActivityAtUtc";
        var sortDirection = ValidSortDirections.Contains(request.SortDirection ?? "") ? request.SortDirection! : "desc";

        return request with
        {
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        };
    }
}
