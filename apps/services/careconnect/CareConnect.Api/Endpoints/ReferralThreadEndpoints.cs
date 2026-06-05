using CareConnect.Application.Interfaces;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// Public referral thread — token-authenticated, no login required.
/// Both the law firm (referrer) and the provider use the same HMAC-signed view token
/// to access referral status and post comments. The token IS the authentication.
/// Route: /api/public/referrals/thread   (proxied via gateway as /careconnect/api/public/referrals/thread)
/// </summary>
public static class ReferralThreadEndpoints
{
    public static void MapReferralThreadEndpoints(this WebApplication app)
    {
        // ── GET /api/public/referrals/thread?token=... ──────────────────────
        // Returns referral summary + comment thread for the given token.
        app.MapGet("/api/public/referrals/thread", async (
            string              token,
            CareConnectDbContext db,
            IReferralEmailService emailSvc,
            CancellationToken   ct) =>
        {
            var tokenResult = emailSvc.ValidateViewToken(token);
            if (tokenResult is null)
                return Results.Problem(statusCode: 404, detail: "Token is invalid or expired.");

            var referral = await db.Referrals
                .Include(r => r.Provider)
                .FirstOrDefaultAsync(r => r.Id == tokenResult.ReferralId, ct);

            if (referral is null || referral.TokenVersion != tokenResult.TokenVersion)
                return Results.Problem(statusCode: 404, detail: "Referral not found or token has been revoked.");

            var comments = await db.ReferralComments
                .Where(c => c.ReferralId == referral.Id)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.SenderType,
                    c.SenderName,
                    c.Message,
                    c.CreatedAt,
                })
                .ToListAsync(ct);

            var attachments = await db.ReferralAttachments
                .Where(a => a.ReferralId == referral.Id)
                .OrderBy(a => a.CreatedAtUtc)
                .Select(a => new
                {
                    id            = a.Id,
                    fileName      = a.FileName,
                    contentType   = a.ContentType,
                    fileSizeBytes = a.FileSizeBytes,
                })
                .ToListAsync(ct);

            var provName = referral.Provider is not null
                ? (string.IsNullOrWhiteSpace(referral.Provider.OrganizationName)
                    ? referral.Provider.Name
                    : referral.Provider.OrganizationName)
                : "Provider";

            return Results.Ok(new
            {
                referralId    = referral.Id,
                tenantId      = referral.TenantId,
                status        = referral.Status,
                // Patient information
                clientName    = $"{referral.ClientFirstName} {referral.ClientLastName}".Trim(),
                clientPhone   = referral.ClientPhone,
                clientEmail   = referral.ClientEmail,
                clientDob     = referral.ClientDob.HasValue
                    ? referral.ClientDob.Value.ToString("MM/dd/yyyy")
                    : null,
                caseNumber    = referral.CaseNumber,
                // Referral metadata
                service       = referral.RequestedService,
                urgency       = referral.Urgency,
                notes         = referral.Notes,
                providerName  = provName,
                // Law firm / referrer information
                referrerName  = referral.ReferrerName,
                referrerEmail = referral.ReferrerEmail,
                createdAt     = referral.CreatedAtUtc,
                comments,
                attachments,
            });
        }).AllowAnonymous();

        // ── POST /api/public/referrals/thread/comments?token=... ────────────
        // Adds a comment and emails the other party.
        app.MapPost("/api/public/referrals/thread/comments", async (
            string              token,
            PostCommentRequest  req,
            CareConnectDbContext db,
            IReferralEmailService emailSvc,
            ILoggerFactory      loggerFactory,
            CancellationToken   ct) =>
        {
            var logger = loggerFactory.CreateLogger("CareConnect.ReferralThread");

            if (string.IsNullOrWhiteSpace(req.SenderType) ||
                (req.SenderType != "referrer" && req.SenderType != "provider"))
                return Results.BadRequest(new { error = "senderType must be 'referrer' or 'provider'." });

            if (string.IsNullOrWhiteSpace(req.SenderName) || req.SenderName.Length > 200)
                return Results.BadRequest(new { error = "senderName is required and must be 200 characters or fewer." });

            if (string.IsNullOrWhiteSpace(req.Message) || req.Message.Length > 4000)
                return Results.BadRequest(new { error = "message is required and must be 4000 characters or fewer." });

            var tokenResult = emailSvc.ValidateViewToken(token);
            if (tokenResult is null)
                return Results.Problem(statusCode: 404, detail: "Token is invalid or expired.");

            var referral = await db.Referrals
                .Include(r => r.Provider)
                .FirstOrDefaultAsync(r => r.Id == tokenResult.ReferralId, ct);

            if (referral is null || referral.TokenVersion != tokenResult.TokenVersion)
                return Results.Problem(statusCode: 404, detail: "Referral not found or token has been revoked.");

            var comment = new ReferralComment
            {
                Id         = Guid.NewGuid(),
                TenantId   = referral.TenantId,
                ReferralId = referral.Id,
                SenderType = req.SenderType,
                SenderName = req.SenderName.Trim(),
                Message    = req.Message.Trim(),
                CreatedAt  = DateTime.UtcNow,
            };

            db.ReferralComments.Add(comment);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "ReferralThread: comment posted on referral {ReferralId} by {SenderType} '{SenderName}'.",
                referral.Id, req.SenderType, req.SenderName);

            // Fire-and-observe: notify the other party by email
            _ = Task.Run(async () =>
            {
                try
                {
                    await emailSvc.SendCommentNotificationAsync(referral, comment, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "ReferralThread: failed to send comment notification for referral {ReferralId}.",
                        referral.Id);
                }
            }, CancellationToken.None);

            return Results.Created($"/api/public/referrals/thread/comments/{comment.Id}", new
            {
                comment.Id,
                comment.SenderType,
                comment.SenderName,
                comment.Message,
                comment.CreatedAt,
            });
        }).AllowAnonymous();
    }

    private sealed record PostCommentRequest(string SenderType, string SenderName, string Message);
}
