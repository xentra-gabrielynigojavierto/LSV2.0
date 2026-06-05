using System.Security.Claims;
using Identity.Api.Helpers;
using Identity.Application;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Services;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // ── POST /api/auth/login ─────────────────────────────────────────────
        // Anonymous. Validates credentials and returns AccessToken + session envelope.
        // The Next.js BFF receives this response, stores the token in an HttpOnly
        // cookie, and forwards only the session envelope (no raw token) to the browser.
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            HttpContext httpContext,
            IAuthService authService,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Identity.Api.Auth.Login");
            try
            {
                var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                      ?? httpContext.Connection.RemoteIpAddress?.ToString();
                var response = await authService.LoginAsync(request, ip, ct);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Problem("Invalid credentials.", statusCode: 401);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
            catch (Exception ex)
            {
                // Guardrail: previously, a DB-level failure (e.g. missing column from
                // an unapplied migration) bubbled up as an unmapped 500 that the BFF
                // surfaced as "Invalid credentials.", making login regressions look
                // like password problems. Log the full exception so the next time
                // login breaks for an infrastructure reason it is diagnosable in
                // minutes from server logs alone.
                logger.LogError(ex,
                    "LoginAsync threw unexpected exception for tenantCode={TenantCode} email={Email}",
                    request?.TenantCode, request?.Email);
                throw;
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-login");

        // ── GET /api/auth/me ─────────────────────────────────────────────────
        // Authenticated (Bearer JWT required).
        // Returns the current session envelope derived from the validated JWT claims.
        // Called server-side by the Next.js BFF /api/auth/me route, which reads the
        // platform_session HttpOnly cookie and forwards it as Authorization: Bearer.
        // Never called directly from browser JS.
        app.MapGet("/api/auth/me", async (
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken ct) =>
        {
            var response = await authService.GetCurrentUserAsync(httpContext.User, ct);
            return Results.Ok(response);
        })
        .RequireAuthorization();

        // ── POST /api/auth/logout ────────────────────────────────────────────
        // Anonymous (JWT may already be expired at logout time).
        // Backend is stateless — real logout is cookie deletion on the Next.js BFF.
        // Emits identity.user.logout for HIPAA audit trail completeness.
        app.MapPost("/api/auth/logout", (
            HttpContext       httpContext,
            IAuditEventClient auditClient) =>
        {
            // Extract identity from the JWT claim if still present in the request.
            // The token may be expired — we read claims without re-validating the signature.
            var principal  = httpContext.User;
            var userId     = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirstValue("sub");
            var tenantId   = principal.FindFirstValue("tenant_id");
            var email      = principal.FindFirstValue(ClaimTypes.Email)
                          ?? principal.FindFirstValue("email");
            var name       = principal.FindFirstValue(ClaimTypes.Name)
                          ?? principal.FindFirstValue("name")
                          ?? email;

            // Fire-and-observe: emit audit event without gating the logout response.
            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.logout",
                EventCategory = EventCategory.Security,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.User,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = tenantId,
                },
                Actor = new AuditEventActorDto
                {
                    Id        = userId,
                    Type      = ActorType.User,
                    Name      = name,
                    IpAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString(),
                },
                Entity      = userId is not null ? new AuditEventEntityDto { Type = "User", Id = userId } : null,
                Action      = "Logout",
                Description = $"User '{email ?? userId ?? "unknown"}' logged out.",
                IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.logout", userId ?? email ?? "anonymous"),
                Tags = ["auth", "logout", "session"],
            });

            return Results.NoContent();
        })
        .AllowAnonymous();

        // ── GET /api/organizations/my/config ──────────────────────────────────
        // Authenticated. Returns org-level configuration for the caller's organization.
        // LS-LIENS-UI-011: Provider mode (sell vs manage) is sourced from here.
        app.MapGet("/api/organizations/my/config", async (
            HttpContext        httpContext,
            IdentityDbContext  db,
            CancellationToken  ct) =>
        {
            var orgIdStr = httpContext.User.FindFirstValue("org_id");
            if (!Guid.TryParse(orgIdStr, out var orgId))
                return Results.Ok(new
                {
                    organizationId = (string?)null,
                    productCode    = "LIENS",
                    settings       = new { providerMode = ProviderModes.Sell }
                });

            var org = await db.Organizations
                .AsNoTracking()
                .Where(o => o.Id == orgId && o.IsActive)
                .Select(o => new { o.Id, o.ProviderMode })
                .FirstOrDefaultAsync(ct);

            if (org is null)
                return Results.Ok(new
                {
                    organizationId = orgIdStr,
                    productCode    = "LIENS",
                    settings       = new { providerMode = ProviderModes.Sell }
                });

            return Results.Ok(new
            {
                organizationId = org.Id.ToString(),
                productCode    = "LIENS",
                settings       = new { providerMode = ProviderModes.Normalize(org.ProviderMode) }
            });
        })
        .RequireAuthorization();

        // ── POST /api/auth/accept-invite ─────────────────────────────────────
        // Anonymous. Accepts an invitation token, sets a new password, and
        // activates the invited user account.
        //
        // Flow:
        //   1. Hash the raw token with SHA-256.
        //   2. Look up the UserInvitation by token hash.
        //   3. Validate: status == PENDING and not expired.
        //   4. Set the user's password and mark them active.
        //   5. Mark the invitation accepted.
        //   6. Emit identity.user.invite_accepted audit event.
        app.MapPost("/api/auth/accept-invite", async (
            AcceptInviteRequest                    body,
            IdentityDbContext                      db,
            IPasswordHasher                        passwordHasher,
            IAuditEventClient                      auditClient,
            IOptions<NotificationsServiceOptions>  notifOptions,
            CancellationToken                      ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Token))
                return Results.BadRequest(new { error = "token is required." });
            if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 8)
                return Results.BadRequest(new { error = "newPassword must be at least 8 characters." });

            // Hash the raw token the same way InviteUser stored it.
            var tokenHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(body.Token)));

            var invitation = await db.UserInvitations
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);

            if (invitation is null)
                return Results.BadRequest(new { error = "Invalid or expired invitation token." });

            if (invitation.Status != Identity.Domain.UserInvitation.Statuses.Pending)
                return Results.BadRequest(new
                {
                    error = invitation.Status == Identity.Domain.UserInvitation.Statuses.Accepted
                        ? "This invitation has already been accepted."
                        : "This invitation is no longer valid.",
                });

            if (invitation.IsExpired())
                return Results.BadRequest(new { error = "This invitation has expired. Please request a new one." });

            var user = invitation.User;
            if (user is null)
                return Results.Problem("User record not found for this invitation.", statusCode: 500);

            // Set the new password and activate the account.
            var passwordHash = passwordHasher.Hash(body.NewPassword);
            user.SetPassword(passwordHash);
            user.Activate();

            // Auto-assign the user to their tenant's organization if they don't
            // already have a membership. Invited users (tenant user / tenant admin)
            // always belong to the tenant's primary org — without this they land
            // on the /no-org wall immediately after their first login.
            var hasMembership = await db.UserOrganizationMemberships
                .AnyAsync(m => m.UserId == user.Id, ct);

            if (!hasMembership)
            {
                var tenantOrg = await db.Organizations
                    .Where(o => o.TenantId == user.TenantId && o.IsActive)
                    .OrderBy(o => o.Name)
                    .FirstOrDefaultAsync(ct);

                if (tenantOrg is not null)
                {
                    var membership = UserOrganizationMembership.Create(
                        userId:         user.Id,
                        organizationId: tenantOrg.Id,
                        memberRole:     MemberRole.Member);
                    membership.SetPrimary();
                    db.UserOrganizationMemberships.Add(membership);
                }
            }

            // Mark the invitation accepted.
            invitation.Accept();

            await db.SaveChangesAsync(ct);

            // Emit audit event (fire-and-observe).
            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.invite_accepted",
                EventCategory = EventCategory.Administrative,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.User,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = user.TenantId.ToString(),
                },
                Actor       = new AuditEventActorDto { Type = ActorType.User, Id = user.Id.ToString() },
                Entity      = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
                Action      = "InviteAccepted",
                Description = $"User '{user.Email}' accepted invitation and activated account in tenant {user.TenantId}.",
                IdempotencyKey = LegalSynq.AuditClient.IdempotencyKey.For(
                    "identity-service", "identity.user.invite_accepted", invitation.Id.ToString()),
                Tags = ["user-management", "invite", "activation"],
            });

            // LS-ID-TNT-016-01: Build tenant portal base URL so the frontend can redirect
            // the user to the correct subdomain login page after accepting the invite.
            var inviteTenant      = await db.Tenants.FindAsync([user.TenantId], ct);
            var tenantPortalUrl   = TenantPortalUrlHelper.BuildBaseUrl(inviteTenant, notifOptions.Value);

            return Results.Ok(new
            {
                message         = "Invitation accepted. Your account is now active.",
                tenantPortalUrl,
            });
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-token-exchange");

        // ── POST /api/auth/change-password ───────────────────────────────────
        // Authenticated. Verifies the caller's current password then replaces it.
        //
        // Flow:
        //   1. Extract user id from the validated JWT (sub claim).
        //   2. Validate request body (currentPassword, newPassword length ≥ 8).
        //   3. Load the user record from the database.
        //   4. Verify currentPassword against the stored bcrypt hash.
        //   5. Hash the new password and call user.SetPassword().
        //   6. Persist changes and emit identity.user.password_changed audit event.
        app.MapPost("/api/auth/change-password", async (
            ChangePasswordRequest body,
            HttpContext           httpContext,
            IdentityDbContext     db,
            IPasswordHasher       passwordHasher,
            IAuditEventClient     auditClient,
            CancellationToken     ct) =>
        {
            var userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub");

            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.Problem("Invalid or missing user identity.", statusCode: 401);

            if (string.IsNullOrWhiteSpace(body.CurrentPassword))
                return Results.BadRequest(new { error = "currentPassword is required." });

            if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 8)
                return Results.BadRequest(new { error = "newPassword must be at least 8 characters." });

            if (body.CurrentPassword == body.NewPassword)
                return Results.BadRequest(new { error = "New password must differ from the current password." });

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null)
                return Results.Problem("User record not found.", statusCode: 404);

            if (!passwordHasher.Verify(body.CurrentPassword, user.PasswordHash))
                return Results.Problem("Current password is incorrect.", statusCode: 400);

            var newHash = passwordHasher.Hash(body.NewPassword);
            user.SetPassword(newHash);
            await db.SaveChangesAsync(ct);

            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.password_changed",
                EventCategory = EventCategory.Security,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.User,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = user.TenantId.ToString(),
                },
                Actor       = new AuditEventActorDto
                {
                    Id        = user.Id.ToString(),
                    Type      = ActorType.User,
                    Name      = user.Email,
                    IpAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString(),
                },
                Entity      = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
                Action      = "PasswordChanged",
                Description = $"User '{user.Email}' changed their password.",
                IdempotencyKey = LegalSynq.AuditClient.IdempotencyKey.ForWithTimestamp(
                    now, "identity-service", "identity.user.password_changed", user.Id.ToString()),
                Tags = ["auth", "password", "security"],
            });

            return Results.Ok(new { message = "Password changed successfully." });
        })
        .RequireAuthorization();

        // ── PATCH /api/profile/avatar ─────────────────────────────────────────
        // Authenticated. Stores the document ID of an already-uploaded avatar.
        // The actual file upload goes directly to the documents service via BFF.
        app.MapPatch("/api/profile/avatar", async (
            SetAvatarRequest   body,
            HttpContext        httpContext,
            IUserRepository    userRepo,
            IAuditEventClient  auditClient,
            CancellationToken  ct) =>
        {
            var userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub");

            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.Problem("Invalid or missing user identity.", statusCode: 401);

            if (!Guid.TryParse(body.DocumentId, out var documentId))
                return Results.BadRequest(new { error = "documentId must be a valid UUID." });

            await userRepo.UpdateAvatarAsync(userId, documentId, ct);

            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.avatar_set",
                EventCategory = EventCategory.DataChange,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.User,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant },
                Actor         = new AuditEventActorDto
                {
                    Id        = userIdStr,
                    Type      = ActorType.User,
                    IpAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString(),
                },
                Entity         = new AuditEventEntityDto { Type = "User", Id = userIdStr! },
                Action         = "AvatarSet",
                Description    = $"User updated their profile picture (document {documentId}).",
                IdempotencyKey = LegalSynq.AuditClient.IdempotencyKey.ForWithTimestamp(
                    now, "identity-service", "identity.user.avatar_set", userIdStr ?? ""),
                Tags = ["profile", "avatar"],
            });

            return Results.Ok(new { avatarDocumentId = documentId });
        })
        .RequireAuthorization();

        // ── DELETE /api/profile/avatar ────────────────────────────────────────
        // Authenticated. Clears the user's avatar document reference.
        app.MapDelete("/api/profile/avatar", async (
            HttpContext        httpContext,
            IUserRepository    userRepo,
            IAuditEventClient  auditClient,
            CancellationToken  ct) =>
        {
            var userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub");

            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.Problem("Invalid or missing user identity.", statusCode: 401);

            await userRepo.UpdateAvatarAsync(userId, null, ct);

            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.avatar_removed",
                EventCategory = EventCategory.DataChange,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.User,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant },
                Actor         = new AuditEventActorDto
                {
                    Id        = userIdStr,
                    Type      = ActorType.User,
                    IpAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString(),
                },
                Entity         = new AuditEventEntityDto { Type = "User", Id = userIdStr! },
                Action         = "AvatarRemoved",
                Description    = "User removed their profile picture.",
                IdempotencyKey = LegalSynq.AuditClient.IdempotencyKey.ForWithTimestamp(
                    now, "identity-service", "identity.user.avatar_removed", userIdStr ?? ""),
                Tags = ["profile", "avatar"],
            });

            return Results.NoContent();
        })
        .RequireAuthorization();

        // ── PATCH /api/profile/phone ──────────────────────────────────────────
        // Authenticated. Lets the signed-in user set or clear their primary
        // phone number. Phones are normalised to E.164 before persisting so
        // the notifications service can dispatch SMS without further reformat.
        // Pass an empty/whitespace string (or null) to clear the field.
        app.MapPatch("/api/profile/phone", async (
            SetPhoneRequest    body,
            HttpContext        httpContext,
            IUserRepository    userRepo,
            IAuditEventClient  auditClient,
            CancellationToken  ct) =>
        {
            var userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub");

            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.Problem("Invalid or missing user identity.", statusCode: 401);

            var (ok, normalised, error) = PhoneNumber.TryNormalise(body.Phone);
            if (!ok)
                return Results.BadRequest(new { error });

            bool changed;
            try
            {
                changed = await userRepo.UpdatePhoneAsync(userId, normalised, ct);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("User record not found.", statusCode: 404);
            }

            if (changed)
            {
                var now = DateTimeOffset.UtcNow;
                _ = auditClient.IngestAsync(new IngestAuditEventRequest
                {
                    EventType     = normalised is null
                        ? "identity.user.phone_cleared"
                        : "identity.user.phone_set",
                    EventCategory = EventCategory.DataChange,
                    SourceSystem  = "identity-service",
                    SourceService = "auth-api",
                    Visibility    = VisibilityScope.User,
                    Severity      = SeverityLevel.Info,
                    OccurredAtUtc = now,
                    Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant },
                    Actor         = new AuditEventActorDto
                    {
                        Id        = userIdStr,
                        Type      = ActorType.User,
                        IpAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                                 ?? httpContext.Connection.RemoteIpAddress?.ToString(),
                    },
                    Entity         = new AuditEventEntityDto { Type = "User", Id = userIdStr! },
                    Action         = normalised is null ? "PhoneCleared" : "PhoneSet",
                    Description    = normalised is null
                        ? "User cleared their primary phone number."
                        : "User updated their primary phone number.",
                    IdempotencyKey = LegalSynq.AuditClient.IdempotencyKey.ForWithTimestamp(
                        now, "identity-service", "identity.user.phone_changed", userIdStr ?? ""),
                    Tags = ["profile", "phone"],
                });
            }

            return Results.Ok(new { phone = normalised });
        })
        .RequireAuthorization();

        // ── POST /api/auth/password-reset/confirm ─────────────────────────────
        // Anonymous. Accepts a password-reset token (admin-triggered), validates it,
        // sets a new password, and invalidates all existing sessions (SessionVersion++).
        //
        // Flow:
        //   1. Hash the raw token with SHA-256.
        //   2. Look up the PasswordResetToken by hash.
        //   3. Validate: status == PENDING and not expired.
        //   4. Set the user's new password (User.SetPassword increments SessionVersion).
        //   5. Mark the token as used.
        //   6. Emit identity.user.password_reset_completed audit event.
        app.MapPost("/api/auth/password-reset/confirm", async (
            PasswordResetConfirmRequest body,
            IdentityDbContext           db,
            IPasswordHasher             passwordHasher,
            IAuditEventClient           auditClient,
            CancellationToken           ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Token))
                return Results.BadRequest(new { error = "token is required." });
            if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 8)
                return Results.BadRequest(new { error = "newPassword must be at least 8 characters." });

            var tokenHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(body.Token)));

            var resetToken = await db.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

            if (resetToken is null)
                return Results.BadRequest(new { error = "Invalid or expired reset token." });

            if (resetToken.Status != PasswordResetToken.Statuses.Pending)
                return Results.BadRequest(new
                {
                    error = resetToken.Status == PasswordResetToken.Statuses.Used
                        ? "This reset link has already been used."
                        : "This reset link is no longer valid.",
                });

            if (resetToken.IsExpired())
                return Results.BadRequest(new { error = "This reset link has expired. Please ask an admin to send a new one." });

            var user = resetToken.User;
            if (user is null)
                return Results.Problem("User record not found for this reset token.", statusCode: 500);

            // SetPassword hashes the new password and increments SessionVersion,
            // which invalidates all existing JWTs for this user.
            var passwordHash = passwordHasher.Hash(body.NewPassword);
            user.SetPassword(passwordHash);
            resetToken.MarkUsed();

            await db.SaveChangesAsync(ct);

            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.password_reset_completed",
                EventCategory = EventCategory.Security,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.User,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = user.TenantId.ToString(),
                },
                Actor = new AuditEventActorDto
                {
                    Id   = user.Id.ToString(),
                    Type = ActorType.User,
                    Name = user.Email,
                },
                Entity      = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
                Action      = "PasswordResetCompleted",
                Description = $"Password reset completed for user '{user.Email}' in tenant {user.TenantId}.",
                IdempotencyKey = LegalSynq.AuditClient.IdempotencyKey.ForWithTimestamp(
                    now, "identity-service", "identity.user.password_reset_completed", user.Id.ToString()),
                Tags = ["auth", "security", "password-reset"],
            });

            return Results.Ok(new { message = "Password updated successfully." });
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-token-exchange");

        // ── POST /api/auth/forgot-password ──────────────────────────────────
        // Anonymous. Self-service password reset request.
        // Accepts { tenantCode, email }, validates the user exists, generates a
        // reset token (stored hashed), and returns only a generic success message.
        // The raw token is never exposed in any API response; it must be delivered
        // out-of-band (email/SMS) by the notification service.
        app.MapPost("/api/auth/forgot-password", async (
            ForgotPasswordRequest                    body,
            IdentityDbContext                        db,
            IAuditEventClient                        auditClient,
            INotificationsEmailClient                emailClient,
            IOptions<NotificationsServiceOptions>    notifOptions,
            IWebHostEnvironment                      env,
            ILoggerFactory                           loggerFactory,
            CancellationToken                        ct) =>
        {
            var logger = loggerFactory.CreateLogger(nameof(AuthEndpoints));

            if (string.IsNullOrWhiteSpace(body.TenantCode))
                return Results.BadRequest(new { error = "tenantCode is required." });
            if (string.IsNullOrWhiteSpace(body.Email))
                return Results.BadRequest(new { error = "email is required." });

            logger.LogInformation("[forgot-password] Received request: tenantCode={TenantCode}, email={Email}",
                body.TenantCode, body.Email);

            var tenant = await db.Tenants
                .FirstOrDefaultAsync(t => t.Code == body.TenantCode && t.IsActive, ct);

            if (tenant is null && !string.IsNullOrWhiteSpace(body.Subdomain))
            {
                var subNorm = body.Subdomain.ToLowerInvariant().Trim();
                logger.LogInformation("[forgot-password] Code lookup missed for {Code}, trying subdomain {Subdomain}", body.TenantCode, subNorm);
                tenant = await db.Tenants
                    .FirstOrDefaultAsync(t => t.Subdomain == subNorm && t.IsActive, ct);
            }

            // AUTH-B01: fall back to TenantId when code+subdomain lookup misses
            // (e.g. common portal careconnect-demo.legalsynq.com where the stored
            // code differs from the raw subdomain).
            if (tenant is null && body.TenantId.HasValue)
            {
                logger.LogInformation("[forgot-password] Code+subdomain lookup missed, trying TenantId={TenantId}", body.TenantId.Value);
                tenant = await db.Tenants
                    .FirstOrDefaultAsync(t => t.Id == body.TenantId.Value, ct);
            }

            if (tenant is null)
            {
                logger.LogWarning("[forgot-password] Tenant not found for code={TenantCode}", body.TenantCode);
                return Results.Ok(new { message = "If an account exists with that email, a password reset link has been generated." });
            }

            logger.LogInformation("[forgot-password] Tenant found: {TenantId} ({TenantCode})", tenant.Id, tenant.Code);

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email == body.Email && u.IsActive, ct);

            if (user is null)
            {
                logger.LogWarning("[forgot-password] User not found: email={Email}, tenantId={TenantId}", body.Email, tenant.Id);
                return Results.Ok(new { message = "If an account exists with that email, a password reset link has been generated." });
            }

            logger.LogInformation("[forgot-password] User found: {UserId} ({Email})", user.Id, user.Email);

            var existingTokens = await db.PasswordResetTokens
                .Where(t => t.UserId == user.Id && t.Status == PasswordResetToken.Statuses.Pending)
                .ToListAsync(ct);
            foreach (var old in existingTokens) old.Revoke();

            var rawToken  = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            var tokenHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(rawToken)));

            var resetToken = PasswordResetToken.Create(user.Id, tenant.Id, tokenHash);
            db.PasswordResetTokens.Add(resetToken);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Password reset requested for user {UserId} ({Email}) in tenant {TenantCode}.",
                user.Id, user.Email, tenant.Code);

            // ── Deliver the raw token out-of-band via email ───────────────────
            // The raw token is NEVER returned in any API response. It is only
            // usable by the account owner who receives the email.
            // LS-ID-TNT-016-01: Build tenant-subdomain-aware reset link.
            // tenant is already resolved earlier in this handler (via tenantCode lookup).
            var resetLink = TenantPortalUrlHelper.Build(tenant, "reset-password", rawToken, notifOptions.Value);
            if (resetLink is null)
            {
                logger.LogError(
                    "[forgot-password] Neither PortalBaseDomain nor PortalBaseUrl is configured. " +
                    "Reset email for user {UserId} ({Email}) cannot be sent. " +
                    "Set NotificationsService:PortalBaseDomain (or PortalBaseUrl) in configuration.",
                    user.Id, user.Email);
            }
            else
            {
                var displayName  = !string.IsNullOrWhiteSpace(user.FirstName)
                    ? $"{user.FirstName} {user.LastName}".Trim()
                    : user.Email;

                if (env.IsDevelopment())
                {
                    logger.LogInformation(
                        "[forgot-password — dev only] resetLink for {Email}: {ResetLink}",
                        user.Email, resetLink);
                }

                try
                {
                    var (_, emailSent, emailError) = await emailClient.SendPasswordResetEmailAsync(
                        user.Email, displayName, resetLink, user.TenantId, ct);

                    if (!emailSent)
                        logger.LogWarning(
                            "[forgot-password] Password reset email delivery failed for user {UserId} ({Email}): {Error}",
                            user.Id, user.Email, emailError ?? "(unknown)");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "[forgot-password] Exception while sending password reset email for user {UserId} ({Email}).",
                        user.Id, user.Email);
                }
            }

            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.password_reset_requested",
                EventCategory = EventCategory.Security,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.User,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = tenant.Id.ToString(),
                },
                Actor = new AuditEventActorDto
                {
                    Id   = user.Id.ToString(),
                    Type = ActorType.User,
                    Name = user.Email,
                },
                Entity      = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
                Action      = "PasswordResetRequested",
                Description = $"Self-service password reset requested for user '{user.Email}' in tenant {tenant.Code}.",
                IdempotencyKey = LegalSynq.AuditClient.IdempotencyKey.ForWithTimestamp(
                    now, "identity-service", "identity.user.password_reset_requested", user.Id.ToString()),
                Tags = ["auth", "security", "password-reset"],
            });

            return Results.Ok(new
            {
                message = "If an account exists with that email, a password reset link has been generated.",
            });
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-forgot-password");
    }

    private record AcceptInviteRequest(string Token, string NewPassword);
    private record PasswordResetConfirmRequest(string Token, string NewPassword);
    private record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private record SetAvatarRequest(string DocumentId);
    private record SetPhoneRequest(string? Phone);
    private record ForgotPasswordRequest(string TenantCode, string Email, string? Subdomain = null, Guid? TenantId = null);
}
