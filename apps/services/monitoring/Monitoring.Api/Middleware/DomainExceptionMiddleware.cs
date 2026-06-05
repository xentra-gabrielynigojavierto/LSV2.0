using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Api.Endpoints;

namespace Monitoring.Api.Middleware;

/// <summary>
/// Translates domain-level <see cref="ArgumentException"/>s (raised by
/// <c>MonitoredEntity</c> invariants) into clean 400 ProblemDetails responses
/// instead of letting them bubble out as 500s with stack traces. Other
/// exceptions are logged and returned as opaque 500s without leaking
/// internals. JSON deserialization errors are surfaced as 400s by the
/// framework's BadHttpRequestException.
/// </summary>
public sealed class DomainExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<DomainExceptionMiddleware> _logger;

    public DomainExceptionMiddleware(RequestDelegate next, ILogger<DomainExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation(
                "Domain validation rejected request {Method} {Path}: {Reason}",
                context.Request.Method, context.Request.Path, ex.Message);
            await WriteProblemAsync(context, ProblemFactory.BadRequest(ex.Message));
        }
        catch (BadHttpRequestException ex)
        {
            // BadHttpRequestException is raised by Minimal API model binding for
            // malformed JSON, undefined enum strings, type mismatches, etc. The
            // framework's exception message can include internal CLR type names
            // and handler parameter names; do NOT echo it back to the caller.
            // Log full detail server-side and return a stable, generic message.
            _logger.LogInformation(
                "Malformed request {Method} {Path}: {Reason}",
                context.Request.Method, context.Request.Path, ex.Message);
            await WriteProblemAsync(context, ProblemFactory.BadRequest(
                "The request body is malformed or contains invalid values."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            var problem = new ProblemDetails
            {
                Title = "Internal server error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An unexpected error occurred.",
                Type = "https://httpstatuses.com/500",
            };
            await WriteProblemAsync(context, problem);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, ProblemDetails problem)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
