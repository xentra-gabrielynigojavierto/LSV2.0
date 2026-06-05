using MySqlConnector;

namespace Support.Api.Notifications;

/// <summary>
/// Looks up user email addresses from the identity database (<c>idt_Users</c>)
/// using a read-only raw SQL connection.  The connection string is read from
/// <c>ConnectionStrings:IdentityDb</c> — the same key the identity service uses.
///
/// All failures are swallowed and logged at Warning level so that a misconfigured
/// or unreachable identity DB never propagates into support ticket writes.
/// </summary>
public sealed class IdentityUserEmailResolver : IUserEmailResolver
{
    private readonly string? _connectionString;
    private readonly ILogger<IdentityUserEmailResolver> _log;

    public IdentityUserEmailResolver(IConfiguration config, ILogger<IdentityUserEmailResolver> log)
    {
        _connectionString = config.GetConnectionString("IdentityDb");
        _log = log;

        if (string.IsNullOrWhiteSpace(_connectionString))
            _log.LogWarning("ConnectionStrings:IdentityDb is not configured — assigned-user email lookup will be skipped");
    }

    public async Task<string?> ResolveAsync(string userId, string tenantId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(userId))
            return null;

        if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(tenantId, out var tenantGuid))
            return null;

        try
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT `Email` FROM `idt_Users` WHERE `Id` = @id AND `TenantId` = @tenantId LIMIT 1";
            cmd.Parameters.AddWithValue("@id",       userGuid.ToString());
            cmd.Parameters.AddWithValue("@tenantId", tenantGuid.ToString());

            var result = await cmd.ExecuteScalarAsync(ct);
            return result as string;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to resolve email for userId={UserId} tenantId={TenantId}", userId, tenantId);
            return null;
        }
    }

    public async Task<Dictionary<string, string>> ResolveManyAsync(
        IEnumerable<string> userIds,
        string tenantId,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(_connectionString) || !Guid.TryParse(tenantId, out var tenantGuid))
            return result;

        var ids = userIds
            .Where(id => Guid.TryParse(id, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0) return result;

        try
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();

            var paramNames = ids.Select((_, i) => $"@id{i}").ToList();
            cmd.CommandText = $"SELECT `Id`, `Email` FROM `idt_Users` WHERE `TenantId` = @tenantId AND `Id` IN ({string.Join(", ", paramNames)})";
            cmd.Parameters.AddWithValue("@tenantId", tenantGuid.ToString());
            for (var i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue(paramNames[i], ids[i]);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id    = reader.GetString(0);
                var email = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (!string.IsNullOrWhiteSpace(email))
                    result[id] = email;
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to batch-resolve emails for {Count} users tenantId={TenantId}", ids.Count, tenantId);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<List<string>> ResolvePlatformAdminEmailsAsync(CancellationToken ct = default)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(_connectionString))
            return result;

        try
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT `Email` FROM `idt_Users` " +
                "WHERE `UserType` = 'PlatformInternal' AND `IsActive` = 1 AND `IsLocked` = 0";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var email = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(email))
                    result.Add(email);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to resolve platform admin emails");
        }

        return result;
    }
}
