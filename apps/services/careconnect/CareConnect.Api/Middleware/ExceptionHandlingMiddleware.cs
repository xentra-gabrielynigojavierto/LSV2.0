using BuildingBlocks.Exceptions;
using System.Security.Claims;
using System.Text.Json;

namespace CareConnect.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            // BLK-OBS-01: log validation failures with request context for diagnosability.
            var rid = GetRequestId(context);
            _logger.LogWarning(
                "Validation error: RequestId={RequestId} Path={Path} ErrorCount={Count}",
                rid, context.Request.Path, ex.Errors?.Count ?? 0);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = "One or more validation errors occurred.",
                    details = ex.Errors
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (NotFoundException ex)
        {
            // BLK-OBS-01: log 404s so tenant-scoped not-found denials are diagnosable.
            var rid = GetRequestId(context);
            _logger.LogWarning(
                "Not found: RequestId={RequestId} Path={Path} Message={Message}",
                rid, context.Request.Path, ex.Message);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = "NOT_FOUND",
                    message = ex.Message
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (ProductAccessDeniedException pex)
        {
            _logger.LogWarning(
                "Product authorization denied: code={ErrorCode} product={ProductCode} org={OrgId} user={User} path={Path}",
                pex.ErrorCode, pex.ProductCode, pex.OrganizationId,
                context.User.FindFirst("sub")?.Value, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = pex.ErrorCode,
                    message = pex.DenialReason ?? pex.Message,
                    productCode = pex.ProductCode,
                    requiredRoles = pex.RequiredRoles,
                    organizationId = pex.OrganizationId
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }));
        }
        catch (ForbiddenException ex)
        {
            // BLK-OBS-01: enrich with RequestId, Path, and UserId so 403 denials are traceable.
            var rid    = GetRequestId(context);
            var userId = context.User.FindFirst("sub")?.Value;
            _logger.LogWarning(
                "Forbidden: RequestId={RequestId} Path={Path} UserId={UserId} Message={Message}",
                rid, context.Request.Path, userId, ex.Message);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = "FORBIDDEN",
                    message = ex.Message
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (ConflictException ex)
        {
            // BLK-OBS-01: log 409 conflicts with request context.
            var rid = GetRequestId(context);
            _logger.LogWarning(
                "Conflict: RequestId={RequestId} Path={Path} ErrorCode={ErrorCode}",
                rid, context.Request.Path, ex.ErrorCode ?? "CONFLICT");

            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = ex.ErrorCode ?? "CONFLICT",
                    message = ex.Message
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (BadHttpRequestException)
        {
            // BLK-OBS-01: log malformed-body rejections.
            var rid = GetRequestId(context);
            _logger.LogWarning(
                "Bad request (malformed body): RequestId={RequestId} Path={Path}",
                rid, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = "BAD_REQUEST",
                    message = "The request body is invalid or malformed."
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (Exception ex)
        {
            // BLK-OBS-01: enrich unhandled exception log with RequestId, Path, and UserId.
            var rid    = GetRequestId(context);
            var userId = context.User.FindFirst("sub")?.Value;
            _logger.LogError(ex,
                "Unhandled exception: RequestId={RequestId} Path={Path} UserId={UserId} Message={Message}",
                rid, context.Request.Path, userId, ex.Message);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An unexpected error occurred. Please try again later."
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
    }

    /// <summary>
    /// BLK-OBS-01: Resolves the correlation / request ID for structured log enrichment.
    /// Uses Items["CorrelationId"] set by CorrelationIdMiddleware; falls back to TraceIdentifier.
    /// </summary>
    private static string GetRequestId(HttpContext context) =>
        context.Items.TryGetValue("CorrelationId", out var v) && v is string s && s.Length > 0
            ? s
            : context.TraceIdentifier;
}
