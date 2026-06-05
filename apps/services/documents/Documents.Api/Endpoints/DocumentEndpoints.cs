using Documents.Api.Middleware;
using Documents.Application.DTOs;
using Documents.Application.Exceptions;
using Documents.Application.Models;
using Documents.Application.Services;
using Documents.Infrastructure.Auth;
using Documents.Infrastructure.Observability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Documents.Api.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var docs = app.MapGroup("/documents")
            .RequireAuthorization()
            .WithTags("Documents");

        // ── POST /documents ──────────────────────────────────────────────────
        docs.MapPost("/", async (
            HttpContext    ctx,
            DocumentService svc,
            IOptions<DocumentServiceOptions> docOpts,
            CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);

            if (!ctx.Request.HasFormContentType)
                return Results.BadRequest(new { error = "VALIDATION_ERROR", message = "Multipart/form-data required" });

            var form = await ctx.Request.ReadFormAsync(ct);

            if (!Guid.TryParse(form["tenantId"], out var tenantId) ||
                !Guid.TryParse(form["documentTypeId"], out var docTypeId))
            {
                return Results.BadRequest(new
                {
                    error   = "VALIDATION_ERROR",
                    message = "Request validation failed",
                    details = new Dictionary<string, string[]>
                    {
                        ["tenantId"]       = new[] { "Required UUID" },
                        ["documentTypeId"] = new[] { "Required UUID" },
                    },
                });
            }

            var file = form.Files["file"];
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "FILE_VALIDATION_ERROR", message = "File is required and must not be empty" });

            // ── Early upload-size check (before any service or storage I/O) ──
            var uploadLimitBytes = (long)docOpts.Value.MaxUploadSizeMb * 1_048_576L;
            if (file.Length > uploadLimitBytes)
            {
                ScanMetrics.UploadFileTooLargeTotal.Inc();
                var corrId = ctx.GetCorrelationId();
                ctx.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("DocumentEndpoints")
                    .LogWarning(
                        "Upload rejected — file too large (early): File={File} SizeBytes={Size} LimitMb={Limit} CorrelationId={Corr}",
                        file.FileName, file.Length, docOpts.Value.MaxUploadSizeMb, corrId);
                return Results.Json(
                    new
                    {
                        error         = "FILE_TOO_LARGE",
                        message       = $"File size {file.Length:N0} bytes ({file.Length / 1_048_576.0:F1} MB) exceeds the maximum upload limit of {docOpts.Value.MaxUploadSizeMb} MB",
                        fileSizeBytes = file.Length,
                        limitMb       = docOpts.Value.MaxUploadSizeMb,
                        correlationId = corrId,
                    },
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            var req = new CreateDocumentRequest
            {
                TenantId       = tenantId,
                ProductId      = form["productId"].ToString(),
                ReferenceId    = form["referenceId"].ToString(),
                ReferenceType  = form["referenceType"].ToString(),
                DocumentTypeId = docTypeId,
                Title          = form["title"].ToString(),
                Description    = form["description"].ToString() is { Length: > 0 } d ? d : null,
            };

            // Validate file magic bytes against the declared MIME type before uploading
            await using (var signatureStream = file.OpenReadStream())
                await DocumentService.ValidateFileSignatureAsync(signatureStream, file.ContentType, ct);

            await using var stream = file.OpenReadStream();
            var result = await svc.CreateAsync(req, stream, file.FileName, file.ContentType, file.Length, reqCtx, ct);

            return Results.Created($"/documents/{result.Id}", new { data = result });
        })
        .WithName("UploadDocument")
        .WithSummary("Upload a new document")
        .DisableAntiforgery();

        // ── GET /documents ────────────────────────────────────────────────────
        docs.MapGet("/", async (
            [FromQuery] string? productId,
            [FromQuery] string? referenceId,
            [FromQuery] string? referenceType,
            [FromQuery] string? status,
            [FromQuery] int limit  = 50,
            [FromQuery] int offset = 0,
            HttpContext ctx = default!,
            DocumentService svc = default!,
            CancellationToken ct = default) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);

            var req = new ListDocumentsRequest
            {
                ProductId     = productId,
                ReferenceId   = referenceId,
                ReferenceType = referenceType,
                Status        = status,
                Limit         = Math.Clamp(limit, 1, 200),
                Offset        = Math.Max(offset, 0),
            };

            var result = await svc.ListAsync(req, reqCtx, ct);
            return Results.Ok(result);
        })
        .WithName("ListDocuments")
        .WithSummary("List documents for the authenticated tenant");

        // ── GET /documents/{id} ───────────────────────────────────────────────
        docs.MapGet("/{id:guid}", async (
            Guid id, HttpContext ctx, DocumentService svc, CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);
            var result    = await svc.GetByIdAsync(id, reqCtx, ct);
            return Results.Ok(new { data = result });
        })
        .WithName("GetDocument")
        .WithSummary("Get a document by ID");

        // ── PATCH /documents/{id} ─────────────────────────────────────────────
        docs.MapPatch("/{id:guid}", async (
            Guid id, UpdateDocumentRequest req,
            HttpContext ctx, DocumentService svc, CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);
            var result    = await svc.UpdateAsync(id, req, reqCtx, ct);
            return Results.Ok(new { data = result });
        })
        .WithName("UpdateDocument")
        .WithSummary("Update document metadata");

        // ── DELETE /documents/{id} ────────────────────────────────────────────
        docs.MapDelete("/{id:guid}", async (
            Guid id, HttpContext ctx, DocumentService svc, CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);
            await svc.DeleteAsync(id, reqCtx, ct);
            return Results.NoContent();
        })
        .WithName("DeleteDocument")
        .WithSummary("Soft-delete a document");

        // ── POST /documents/{id}/versions ─────────────────────────────────────
        docs.MapPost("/{id:guid}/versions", async (
            Guid id, HttpContext ctx, DocumentService svc,
            IOptions<DocumentServiceOptions> docOpts,
            CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);

            if (!ctx.Request.HasFormContentType)
                return Results.BadRequest(new { error = "VALIDATION_ERROR", message = "Multipart/form-data required" });

            var form  = await ctx.Request.ReadFormAsync(ct);
            var file  = form.Files["file"];
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "FILE_VALIDATION_ERROR", message = "File is required and must not be empty" });

            // ── Early upload-size check (before any service or storage I/O) ──
            var versionUploadLimitBytes = (long)docOpts.Value.MaxUploadSizeMb * 1_048_576L;
            if (file.Length > versionUploadLimitBytes)
            {
                ScanMetrics.UploadFileTooLargeTotal.Inc();
                var corrId = ctx.GetCorrelationId();
                ctx.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("DocumentEndpoints")
                    .LogWarning(
                        "Version upload rejected — file too large (early): File={File} SizeBytes={Size} LimitMb={Limit} CorrelationId={Corr}",
                        file.FileName, file.Length, docOpts.Value.MaxUploadSizeMb, corrId);
                return Results.Json(
                    new
                    {
                        error         = "FILE_TOO_LARGE",
                        message       = $"File size {file.Length:N0} bytes ({file.Length / 1_048_576.0:F1} MB) exceeds the maximum upload limit of {docOpts.Value.MaxUploadSizeMb} MB",
                        fileSizeBytes = file.Length,
                        limitMb       = docOpts.Value.MaxUploadSizeMb,
                        correlationId = corrId,
                    },
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            var req = new UploadDocumentVersionRequest { Label = form["label"].ToString() is { Length: > 0 } l ? l : null };

            // Validate file magic bytes against the declared MIME type before uploading
            await using (var signatureStream = file.OpenReadStream())
                await DocumentService.ValidateFileSignatureAsync(signatureStream, file.ContentType, ct);

            await using var stream = file.OpenReadStream();
            var result = await svc.CreateVersionAsync(id, req, stream, file.FileName, file.ContentType, file.Length, reqCtx, ct);

            return Results.Created($"/documents/{id}/versions/{result.Id}", new { data = result });
        })
        .WithName("UploadDocumentVersion")
        .WithSummary("Upload a new version of a document")
        .DisableAntiforgery();

        // ── GET /documents/{id}/versions ──────────────────────────────────────
        docs.MapGet("/{id:guid}/versions", async (
            Guid id, HttpContext ctx, DocumentService svc, CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);
            var result    = await svc.ListVersionsAsync(id, reqCtx, ct);
            return Results.Ok(new { data = result });
        })
        .WithName("ListDocumentVersions")
        .WithSummary("List all versions of a document");

        // ── POST /documents/{id}/view-url ─────────────────────────────────────
        docs.MapPost("/{id:guid}/view-url", async (
            Guid id, HttpContext ctx, AccessTokenService svc, CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);
            var result    = await svc.IssueAsync(id, "view", reqCtx, ct);
            return Results.Ok(new { data = result });
        })
        .WithName("RequestViewUrl")
        .WithSummary("Request a view access token");

        // ── POST /documents/{id}/download-url ─────────────────────────────────
        docs.MapPost("/{id:guid}/download-url", async (
            Guid id, HttpContext ctx, AccessTokenService svc, CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);
            var result    = await svc.IssueAsync(id, "download", reqCtx, ct);
            return Results.Ok(new { data = result });
        })
        .WithName("RequestDownloadUrl")
        .WithSummary("Request a download access token");

        // ── PUT /documents/{id}/logo-registration ─────────────────────────────
        docs.MapPut("/{id:guid}/logo-registration", async (
            Guid id, HttpContext ctx, DocumentService svc, CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);
            await svc.RegisterAsLogoAsync(id, reqCtx, ct);
            return Results.NoContent();
        })
        .WithName("RegisterDocumentAsLogo")
        .WithSummary("Register a document as the published tenant logo (admin only)");

        // ── DELETE /documents/{id}/logo-registration ───────────────────────────
        docs.MapDelete("/{id:guid}/logo-registration", async (
            Guid id, HttpContext ctx, DocumentService svc, CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);
            await svc.UnregisterLogoAsync(id, reqCtx, ct);
            return Results.NoContent();
        })
        .WithName("UnregisterDocumentAsLogo")
        .WithSummary("Remove the published logo registration from a document (admin only)");

        // ── DELETE /documents/logo-registration ────────────────────────────────
        // Clears all logo registrations for the tenant (used when the logo is deleted).
        docs.MapDelete("/logo-registration", async (
            HttpContext ctx, DocumentService svc, CancellationToken ct) =>
        {
            var principal = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx    = BuildContext(ctx, principal);
            await svc.ClearTenantLogoRegistrationAsync(reqCtx, ct);
            return Results.NoContent();
        })
        .WithName("ClearTenantLogoRegistration")
        .WithSummary("Clear all logo registrations for the tenant (admin only)");

        // ── GET /documents/{id}/content ───────────────────────────────────────
        docs.MapGet("/{id:guid}/content", async (
            Guid id,
            [FromQuery] string type = "view",
            HttpContext ctx = default!,
            DocumentService svc = default!,
            CancellationToken ct = default) =>
        {
            var principal   = JwtPrincipalExtractor.Extract(ctx.User);
            var reqCtx      = BuildContext(ctx, principal);
            var redirectUrl = await svc.GetContentRedirectAsync(id, type, reqCtx, ct);
            return Results.Redirect(redirectUrl);
        })
        .WithName("GetDocumentContent")
        .WithSummary("Direct authenticated file access — returns 302 redirect");
    }

    private static RequestContext BuildContext(HttpContext ctx, Domain.ValueObjects.Principal principal) =>
        new()
        {
            Principal      = principal,
            CorrelationId  = ctx.GetCorrelationId(),
            IpAddress      = ctx.Connection.RemoteIpAddress?.ToString(),
            UserAgent      = ctx.Request.Headers["User-Agent"].FirstOrDefault(),
            TargetTenantId = Guid.TryParse(
                ctx.Request.Headers["X-Admin-Target-Tenant"].FirstOrDefault(), out var t) ? t : null,
        };
}
