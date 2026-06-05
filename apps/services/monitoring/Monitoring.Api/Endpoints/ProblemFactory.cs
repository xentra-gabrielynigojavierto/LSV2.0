using Microsoft.AspNetCore.Mvc;

namespace Monitoring.Api.Endpoints;

/// <summary>
/// Small helper that produces RFC 7807 ProblemDetails payloads with consistent
/// shape across all monitored entity endpoints.
/// </summary>
internal static class ProblemFactory
{
    public static ProblemDetails BadRequest(
        string detail,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = new ValidationProblemDetails(errors ?? new Dictionary<string, string[]>())
        {
            Title = "Invalid request",
            Status = StatusCodes.Status400BadRequest,
            Detail = detail,
            Type = "https://httpstatuses.com/400",
        };
        return problem;
    }

    public static ProblemDetails NotFound(string detail) => new()
    {
        Title = "Resource not found",
        Status = StatusCodes.Status404NotFound,
        Detail = detail,
        Type = "https://httpstatuses.com/404",
    };
}
