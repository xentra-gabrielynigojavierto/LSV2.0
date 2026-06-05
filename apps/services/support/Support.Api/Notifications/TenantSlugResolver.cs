using MySqlConnector;

namespace Support.Api.Notifications;

public interface ITenantSlugResolver
{
    Task<string?> ResolveAsync(string tenantId, CancellationToken ct = default);
}

/// <summary>
/// Resolves a tenant's short subdomain slug (Subdomain ?? Code) from the
/// <c>tenant_Tenants</c> table using the <c>ConnectionStrings:TenantDb</c>
/// connection string.  Results are cached indefinitely for the process lifetime
/// since tenant slugs are immutable after provisioning.
///
/// A null return means the slug could not be resolved (misconfigured connection
/// string, unknown tenantId, DB error).  Callers fall back to <c>PortalBaseUrl</c>.
/// </summary>
public sealed class TenantDbSlugResolver : ITenantSlugResolver
{
    private readonly string? _connectionString;
    private readonly ILogger<TenantDbSlugResolver> _log;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public TenantDbSlugResolver(IConfiguration config, ILogger<TenantDbSlugResolver> log)
    {
        _connectionString = config.GetConnectionString("TenantDb");
        _log = log;

        if (string.IsNullOrWhiteSpace(_connectionString))
            _log.LogWarning("ConnectionStrings:TenantDb is not configured — tenant slug lookup will be skipped");
    }

    public async Task<string?> ResolveAsync(string tenantId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(tenantId))
            return null;

        if (_cache.TryGetValue(tenantId, out var cached))
            return cached;

        if (!Guid.TryParse(tenantId, out var tenantGuid))
            return null;

        try
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT `Subdomain`, `Code` FROM `tenant_Tenants` WHERE `Id` = @id LIMIT 1";
            cmd.Parameters.AddWithValue("@id", tenantGuid.ToString());

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var subdomain = reader.IsDBNull(0) ? null : reader.GetString(0);
                var code      = reader.IsDBNull(1) ? null : reader.GetString(1);
                var slug      = subdomain ?? code;

                if (!string.IsNullOrWhiteSpace(slug))
                {
                    _cache[tenantId] = slug;
                    return slug;
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to resolve tenant slug for tenantId={TenantId}", tenantId);
        }

        return null;
    }
}
