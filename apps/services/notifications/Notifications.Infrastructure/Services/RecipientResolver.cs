using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// Default <see cref="IRecipientResolver"/> for the notifications service.
///
/// Direct addressing (<c>UserId</c> / <c>Email</c>) resolves inline.
/// Role / Org addressing is delegated to the configured
/// <see cref="IRoleMembershipProvider"/>; if no real provider is wired
/// the empty no-op provider returns no members and the caller treats the
/// fan-out as an empty recipient set.
/// </summary>
public class RecipientResolver : IRecipientResolver
{
    private readonly IRoleMembershipProvider _membership;
    private readonly ILogger<RecipientResolver> _logger;

    public RecipientResolver(IRoleMembershipProvider membership, ILogger<RecipientResolver> logger)
    {
        _membership = membership;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResolvedRecipient>> ResolveAsync(Guid tenantId, JsonElement recipient)
    {
        // Array form: producers may address several roles/orgs in a single
        // submitted notification. Resolve each envelope independently, then
        // dedupe across the union by StableKey so a user that belongs to
        // multiple addressed roles/orgs receives the notification only once.
        if (recipient.ValueKind == JsonValueKind.Array)
        {
            var aggregate = new List<ResolvedRecipient>();
            foreach (var envelope in recipient.EnumerateArray())
            {
                if (envelope.ValueKind != JsonValueKind.Object) continue;
                var part = await ResolveSingleAsync(tenantId, envelope);
                if (part.Count > 0) aggregate.AddRange(part);
            }
            return Dedupe(aggregate);
        }

        if (recipient.ValueKind != JsonValueKind.Object)
            return Array.Empty<ResolvedRecipient>();

        return await ResolveSingleAsync(tenantId, recipient);
    }

    private async Task<IReadOnlyList<ResolvedRecipient>> ResolveSingleAsync(Guid tenantId, JsonElement recipient)
    {
        var mode    = ReadMode(recipient);
        var userId  = ReadString(recipient, "userId");
        var email   = ReadString(recipient, "email");
        var phone   = ReadString(recipient, "phone");
        var roleKey = ReadString(recipient, "roleKey");
        var orgId   = ReadString(recipient, "orgId");

        // Mode is informational; fall back to whichever identifier is populated.
        if (string.Equals(mode, "Role", StringComparison.OrdinalIgnoreCase) ||
            (string.IsNullOrEmpty(mode) && !string.IsNullOrEmpty(roleKey) && string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(phone)))
        {
            if (string.IsNullOrEmpty(roleKey))
            {
                _logger.LogWarning("Role recipient missing roleKey for tenant {TenantId}", tenantId);
                return Array.Empty<ResolvedRecipient>();
            }
            var members = await _membership.GetRoleMembersAsync(tenantId, roleKey, orgId);
            return Dedupe(members);
        }

        if (string.Equals(mode, "Org", StringComparison.OrdinalIgnoreCase) ||
            (string.IsNullOrEmpty(mode) && !string.IsNullOrEmpty(orgId) && string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(phone) && string.IsNullOrEmpty(roleKey)))
        {
            if (string.IsNullOrEmpty(orgId))
            {
                _logger.LogWarning("Org recipient missing orgId for tenant {TenantId}", tenantId);
                return Array.Empty<ResolvedRecipient>();
            }
            var members = await _membership.GetOrgMembersAsync(tenantId, orgId);
            return Dedupe(members);
        }

        // Direct addressing: UserId, Email, or Phone (SMS).
        if (!string.IsNullOrEmpty(userId) || !string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(phone))
        {
            return new[]
            {
                new ResolvedRecipient { UserId = userId, Email = email, Phone = phone, OrgId = orgId }
            };
        }

        return Array.Empty<ResolvedRecipient>();
    }

    /// <summary>
    /// Case-insensitive property read so producers using PascalCase JSON
    /// (e.g. raw <see cref="Contracts.Notifications.NotificationRecipient"/>
    /// serialization) and camelCase JSON (translator output) both resolve.
    /// </summary>
    private static string? ReadString(JsonElement obj, string name)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.NameEquals(name) || string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
            }
        }
        return null;
    }

    /// <summary>
    /// Tolerates both string ("Role") and numeric (2) JSON representations of
    /// <see cref="Contracts.Notifications.RecipientMode"/>; either is valid
    /// depending on the producer's JSON serializer settings.
    /// </summary>
    private static string? ReadMode(JsonElement obj)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "mode", StringComparison.OrdinalIgnoreCase)) continue;
            return prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number when prop.Value.TryGetInt32(out var n) => n switch
                {
                    0 => "UserId",
                    1 => "Email",
                    2 => "Role",
                    3 => "Org",
                    _ => null,
                },
                _ => null,
            };
        }
        return null;
    }

    private static IReadOnlyList<ResolvedRecipient> Dedupe(IReadOnlyList<ResolvedRecipient> members)
    {
        if (members.Count <= 1) return members;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ResolvedRecipient>(members.Count);
        foreach (var m in members)
        {
            if (seen.Add(m.StableKey)) result.Add(m);
        }
        return result;
    }
}

/// <summary>
/// Default no-op <see cref="IRoleMembershipProvider"/>. Production
/// deployments replace this with an identity-backed implementation.
/// </summary>
public class NoOpRoleMembershipProvider : IRoleMembershipProvider
{
    public Task<IReadOnlyList<ResolvedRecipient>> GetRoleMembersAsync(Guid tenantId, string roleKey, string? orgId) =>
        Task.FromResult<IReadOnlyList<ResolvedRecipient>>(Array.Empty<ResolvedRecipient>());

    public Task<IReadOnlyList<ResolvedRecipient>> GetOrgMembersAsync(Guid tenantId, string orgId) =>
        Task.FromResult<IReadOnlyList<ResolvedRecipient>>(Array.Empty<ResolvedRecipient>());
}

/// <summary>
/// In-memory <see cref="IRoleMembershipProvider"/> useful for local dev,
/// integration tests, and seeding fan-out targets without a full identity
/// round-trip. Thread-safe; entries are scoped by tenant.
/// </summary>
public class InMemoryRoleMembershipProvider : IRoleMembershipProvider
{
    private readonly ConcurrentDictionary<string, List<ResolvedRecipient>> _byRole = new();
    private readonly ConcurrentDictionary<string, List<ResolvedRecipient>> _byOrg  = new();

    public void RegisterRoleMember(Guid tenantId, string roleKey, string? orgId, ResolvedRecipient recipient)
    {
        var key = RoleKey(tenantId, roleKey, orgId);
        _byRole.AddOrUpdate(key, _ => new List<ResolvedRecipient> { recipient }, (_, list) =>
        {
            lock (list) { list.Add(recipient); return list; }
        });
    }

    public void RegisterOrgMember(Guid tenantId, string orgId, ResolvedRecipient recipient)
    {
        var key = OrgKey(tenantId, orgId);
        _byOrg.AddOrUpdate(key, _ => new List<ResolvedRecipient> { recipient }, (_, list) =>
        {
            lock (list) { list.Add(recipient); return list; }
        });
    }

    public Task<IReadOnlyList<ResolvedRecipient>> GetRoleMembersAsync(Guid tenantId, string roleKey, string? orgId)
    {
        // Prefer org-scoped entries; fall back to tenant-wide role entries.
        if (!string.IsNullOrEmpty(orgId) && _byRole.TryGetValue(RoleKey(tenantId, roleKey, orgId), out var scoped))
        {
            lock (scoped) return Task.FromResult<IReadOnlyList<ResolvedRecipient>>(scoped.ToList());
        }
        if (_byRole.TryGetValue(RoleKey(tenantId, roleKey, null), out var global))
        {
            lock (global) return Task.FromResult<IReadOnlyList<ResolvedRecipient>>(global.ToList());
        }
        return Task.FromResult<IReadOnlyList<ResolvedRecipient>>(Array.Empty<ResolvedRecipient>());
    }

    public Task<IReadOnlyList<ResolvedRecipient>> GetOrgMembersAsync(Guid tenantId, string orgId)
    {
        if (_byOrg.TryGetValue(OrgKey(tenantId, orgId), out var list))
        {
            lock (list) return Task.FromResult<IReadOnlyList<ResolvedRecipient>>(list.ToList());
        }
        return Task.FromResult<IReadOnlyList<ResolvedRecipient>>(Array.Empty<ResolvedRecipient>());
    }

    private static string RoleKey(Guid tenantId, string roleKey, string? orgId) =>
        $"{tenantId:N}|{roleKey}|{orgId ?? "*"}";

    private static string OrgKey(Guid tenantId, string orgId) => $"{tenantId:N}|{orgId}";
}
