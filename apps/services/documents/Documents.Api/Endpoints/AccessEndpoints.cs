using Documents.Api.Middleware;
using Documents.Application.Exceptions;
using Documents.Application.Services;

namespace Documents.Api.Endpoints;

public static class AccessEndpoints
{
    public static void MapAccessEndpoints(this WebApplication app)
    {
        // GET /access/{token} — unauthenticated token redemption
        app.MapGet("/access/{token}", async (
            string         token,
            HttpContext    ctx,
            AccessTokenService svc,
            CancellationToken ct) =>
        {
            // Validate token format: must be exactly 64 lowercase hex characters
            if (string.IsNullOrEmpty(token) || token.Length != 64 || !IsHex(token))
            {
                return Results.Json(new
                {
                    error   = "TOKEN_INVALID",
                    message = "Access token is invalid or has already been used",
                }, statusCode: 401);
            }

            var correlationId = ctx.GetCorrelationId();

            try
            {
                var redirectUrl = await svc.RedeemAsync(
                    token,
                    ctx.Connection.RemoteIpAddress?.ToString(),
                    ctx.Request.Headers["User-Agent"].FirstOrDefault(),
                    correlationId,
                    ct);

                return Results.Redirect(redirectUrl);
            }
            catch (TokenExpiredException ex)
            {
                return Results.Json(new { error = ex.ErrorCode, message = ex.Message, correlationId },
                    statusCode: 401);
            }
            catch (TokenInvalidException ex)
            {
                return Results.Json(new { error = ex.ErrorCode, message = ex.Message, correlationId },
                    statusCode: 401);
            }
            catch (ScanBlockedException ex)
            {
                return Results.Json(new { error = ex.ErrorCode, message = ex.Message, correlationId },
                    statusCode: 403);
            }
            catch (NotFoundException ex)
            {
                return Results.Json(new { error = ex.ErrorCode, message = ex.Message, correlationId },
                    statusCode: 404);
            }
        })
        .WithName("RedeemAccessToken")
        .WithTags("Access")
        .WithSummary("Redeem an opaque access token for a 302 redirect to the file")
        .AllowAnonymous();
    }

    private static bool IsHex(string s)
    {
        foreach (var c in s)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                return false;
        }
        return true;
    }
}
