using FluentValidation;
using Microsoft.Extensions.Options;
using Support.Api.Auth;
using Support.Api.Configuration;
using Support.Api.Dtos;
using Support.Api.Files;
using Support.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Support.Api.Endpoints;

public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/support/api/tickets/{id:guid}").WithTags("Attachments");

        grp.MapPost("/attachments", async (
            Guid id,
            [FromBody] CreateTicketAttachmentRequest req,
            IValidator<CreateTicketAttachmentRequest> validator,
            ITicketAttachmentService svc,
            CancellationToken ct) =>
        {
            var v = await validator.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try
            {
                var a = await svc.AddAsync(id, req, ct);
                return Results.Created($"/support/api/tickets/{id}/attachments/{a.Id}", a);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (TicketNotFoundException) { return Results.NotFound(); }
            catch (DuplicateAttachmentException)
            {
                return Results.Problem(statusCode: 409, title: "Duplicate attachment",
                    detail: "An attachment with the same document_id is already linked to this ticket.");
            }
        })
        .RequireAuthorization(SupportPolicies.SupportWrite)
        .Produces<TicketAttachmentResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .ProducesValidationProblem();

        grp.MapPost("/attachments/upload", async (
            HttpContext http,
            Guid id,
            ITicketAttachmentService svc,
            CancellationToken ct) =>
        {
            if (!http.Request.HasFormContentType)
            {
                return Results.Problem(
                    statusCode: 400,
                    title: "multipart/form-data required",
                    detail: "Use multipart/form-data with a 'file' part.");
            }

            IFormCollection form;
            try
            {
                form = await http.Request.ReadFormAsync(ct);
            }
            catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException)
            {
                return Results.Problem(statusCode: 400, title: "Invalid multipart payload",
                    detail: ex.Message);
            }

            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            var displayName = form["display_name"].ToString();
            // Note: any `uploaded_by_user_id` form value is intentionally
            // ignored; uploader identity is derived from the JWT inside
            // the service to prevent attribution spoofing.

            try
            {
                var created = await svc.UploadAndAttachAsync(
                    id,
                    file!,
                    string.IsNullOrWhiteSpace(displayName) ? null : displayName,
                    ct);
                return Results.Created(
                    $"/support/api/tickets/{id}/attachments/{created.Id}", created);
            }
            catch (TenantMissingException)
            {
                return Results.Problem(statusCode: 400, title: "Tenant context required");
            }
            catch (TicketNotFoundException)
            {
                return Results.NotFound();
            }
            catch (AttachmentUploadValidationException ex)
            {
                return Results.ValidationProblem(
                    ex.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            }
            catch (DuplicateAttachmentException)
            {
                return Results.Problem(statusCode: 409, title: "Duplicate attachment",
                    detail: "An attachment with the same document_id is already linked to this ticket.");
            }
            catch (SupportFileStorageNotConfiguredException ex)
            {
                return Results.Problem(statusCode: 503,
                    title: "File upload not available", detail: ex.Message);
            }
            catch (SupportFileStorageRemoteException ex)
            {
                return Results.Problem(statusCode: 502,
                    title: "Upstream document service unavailable", detail: ex.Message);
            }
            catch (SupportFileStorageException ex)
            {
                return Results.Problem(statusCode: 500,
                    title: "File upload failed", detail: ex.Message);
            }
        })
        .RequireAuthorization(SupportPolicies.SupportWrite)
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<TicketAttachmentResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status502BadGateway)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .ProducesValidationProblem();

        grp.MapGet("/attachments", async (
            Guid id,
            ITicketAttachmentService svc,
            CancellationToken ct) =>
        {
            try
            {
                var items = await svc.ListAsync(id, ct);
                return Results.Ok(items);
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
            catch (TicketNotFoundException) { return Results.NotFound(); }
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces<List<TicketAttachmentResponse>>()
        .Produces(StatusCodes.Status404NotFound);

        grp.MapGet("/attachments/{attachmentId:guid}/download", async (
            Guid id,
            Guid attachmentId,
            ITicketAttachmentService svc,
            IOptionsMonitor<FileStorageOptions> fileOpts,
            IHttpClientFactory httpClientFactory,
            CancellationToken ct) =>
        {
            try
            {
                var attachment = await svc.GetByIdAsync(id, attachmentId, ct);
                if (attachment is null) return Results.NotFound();

                var opts = fileOpts.CurrentValue;

                if (opts.Mode == FileStorageMode.Local)
                {
                    var rootFull = Path.GetFullPath(opts.LocalRootPath);
                    var dirs     = Directory.GetDirectories(rootFull, attachment.DocumentId, SearchOption.AllDirectories);
                    var docDir   = dirs.FirstOrDefault();
                    if (docDir is null) return Results.NotFound();
                    var candidates = Directory.GetFiles(docDir);
                    if (candidates.Length == 0) return Results.NotFound();
                    var filePath = candidates[0];
                    var stream   = System.IO.File.OpenRead(filePath);
                    await svc.AuditDownloadAsync(attachment, ct);
                    return Results.File(stream,
                        attachment.ContentType ?? "application/octet-stream",
                        attachment.FileName);
                }

                if (opts.Mode == FileStorageMode.DocumentsService)
                {
                    var baseUrl      = opts.DocumentsService.BaseUrl;
                    if (string.IsNullOrWhiteSpace(baseUrl))
                        return Results.Problem(statusCode: 503, title: "File storage not configured");

                    var downloadUrl  = new Uri(new Uri(baseUrl), $"/documents/api/documents/{attachment.DocumentId}/download").ToString();
                    var client       = httpClientFactory.CreateClient(DocumentsServiceFileStorageProvider.HttpClientName);
                    HttpResponseMessage upstream;
                    try
                    {
                        upstream = await client.GetAsync(downloadUrl, ct);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        return Results.Problem(statusCode: 502, title: "Documents Service unavailable");
                    }

                    if (!upstream.IsSuccessStatusCode)
                        return Results.Problem(statusCode: 502, title: "Document download failed",
                            detail: $"Documents Service returned {(int)upstream.StatusCode}.");

                    var contentType = upstream.Content.Headers.ContentType?.ToString()
                                      ?? attachment.ContentType
                                      ?? "application/octet-stream";
                    var stream = await upstream.Content.ReadAsStreamAsync(ct);
                    await svc.AuditDownloadAsync(attachment, ct);
                    return Results.File(stream, contentType, attachment.FileName);
                }

                return Results.Problem(statusCode: 503, title: "File storage not configured",
                    detail: "Configure Support:FileStorage:Mode to enable downloads.");
            }
            catch (TenantMissingException) { return Results.Problem(statusCode: 400, title: "Tenant context required"); }
        })
        .RequireAuthorization(SupportPolicies.SupportRead)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status502BadGateway)
        .Produces(StatusCodes.Status503ServiceUnavailable);
    }
}
