using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Service-token authenticator for <c>IngestAuth:Mode = "ServiceToken"</c>.
///
/// Validates the <c>x-service-token</c> header against a registry of named per-service
/// tokens loaded from <see cref="IngestAuthOptions.ServiceTokens"/>.
///
/// Security properties:
///   - Constant-time comparison (<see cref="CryptographicOperations.FixedTimeEquals"/>)
///     prevents timing-based side-channel attacks when scanning the token registry.
///   - Only entries with <see cref="ServiceTokenEntry.Enabled"/> = true are eligible.
///   - Empty token values in registry entries are skipped (misconfigured entries are inert).
///   - A startup warning is logged when no tokens are configured so operators are alerted
///     before the endpoint silently rejects all callers.
///
/// Extension path to JWT/mTLS:
///   Implement <see cref="IIngestAuthenticator"/> in a new class and register it
///   for the desired mode value. This class and its config schema are unchanged.
/// </summary>
public sealed class ServiceTokenAuthenticator : IIngestAuthenticator
{
    private readonly IngestAuthOptions                    _options;
    private readonly ILogger<ServiceTokenAuthenticator>   _logger;

    // Pre-encoded registry entries for O(n) constant-time scan.
    // Built once at construction to avoid repeated encoding on every request.
    private readonly IReadOnlyList<(byte[] TokenBytes, ServiceTokenEntry Entry)> _registry;

    public ServiceTokenAuthenticator(
        IOptions<IngestAuthOptions>           options,
        ILogger<ServiceTokenAuthenticator>    logger)
    {
        _options = options.Value;
        _logger  = logger;

        // Build the pre-encoded registry at startup — hot path stays allocation-free.
        _registry = _options.ServiceTokens
            .Where(e => e.Enabled && !string.IsNullOrWhiteSpace(e.Token))
            .Select(e => (Encoding.UTF8.GetBytes(e.Token), e))
            .ToList()
            .AsReadOnly();

        if (_registry.Count == 0)
        {
            _logger.LogWarning(
                "ServiceToken auth mode is active but no enabled token entries are configured " +
                "in IngestAuth:ServiceTokens. All ingest requests will be rejected with 401. " +
                "Add at least one enabled entry with a non-empty Token value.");
        }
        else
        {
            _logger.LogInformation(
                "ServiceToken auth initialized — {Count} registered service(s): {Services}",
                _registry.Count,
                string.Join(", ", _registry.Select(r => r.Entry.ServiceName)));
        }
    }

    /// <inheritdoc/>
    public string Mode => "ServiceToken";

    /// <inheritdoc/>
    public Task<AuthResult> AuthenticateAsync(IHeaderDictionary headers, CancellationToken ct = default)
    {
        // ── Extract x-service-token ──────────────────────────────────────────
        if (!headers.TryGetValue(IngestAuthHeaders.ServiceToken, out var tokenValues) ||
            string.IsNullOrWhiteSpace(tokenValues.ToString()))
        {
            return Task.FromResult(AuthResult.Missing);
        }

        // ── Guard: no tokens configured ──────────────────────────────────────
        if (_registry.Count == 0)
        {
            return Task.FromResult(AuthResult.NotConfigured);
        }

        // ── Constant-time scan ───────────────────────────────────────────────
        // Encode the inbound token once, then compare against all registry entries
        // using FixedTimeEquals. We do NOT short-circuit on first failure — we always
        // scan the full registry to avoid leaking the registry size via timing.
        var inboundBytes = Encoding.UTF8.GetBytes(tokenValues.ToString());

        ServiceTokenEntry? matched = null;

        foreach (var (registeredBytes, entry) in _registry)
        {
            // FixedTimeEquals requires equal-length arrays.
            // Lengths will differ for wrong tokens — still compare to prevent length leaking.
            var lengthsMatch = registeredBytes.Length == inboundBytes.Length;

            // Pad shorter array to equal length for constant-time comparison.
            // Always run the comparison regardless of length match.
            var aBytes = lengthsMatch ? inboundBytes   : PadOrTruncate(inboundBytes,   registeredBytes.Length);
            var bBytes = lengthsMatch ? registeredBytes : PadOrTruncate(registeredBytes, inboundBytes.Length);

            if (CryptographicOperations.FixedTimeEquals(aBytes, bBytes) && lengthsMatch)
            {
                matched = entry;
                // Do NOT break — continue to scan remaining entries for constant time.
            }
        }

        // ── Extract optional context headers ─────────────────────────────────
        var sourceSystem  = headers.TryGetValue(IngestAuthHeaders.SourceSystem,  out var ss)   ? ss.ToString()   : null;
        var sourceService = headers.TryGetValue(IngestAuthHeaders.SourceService, out var ssvc)  ? ssvc.ToString() : null;

        // ── Result ───────────────────────────────────────────────────────────
        if (matched is not null)
        {
            _logger.LogDebug(
                "Ingest auth succeeded — ServiceName={ServiceName} SourceSystem={SourceSystem} SourceService={SourceService}",
                matched.ServiceName, sourceSystem, sourceService);

            return Task.FromResult(new AuthResult(
                Succeeded:     true,
                ServiceName:   matched.ServiceName,
                SourceSystem:  sourceSystem,
                SourceService: sourceService));
        }

        _logger.LogWarning(
            "Ingest auth failed — InvalidToken. SourceSystem={SourceSystem} SourceService={SourceService}",
            sourceSystem, sourceService);

        return Task.FromResult(AuthResult.Invalid with
        {
            SourceSystem  = sourceSystem,
            SourceService = sourceService,
        });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns a byte array of exactly <paramref name="targetLength"/> bytes.
    /// Used to normalize array lengths before FixedTimeEquals so the comparison
    /// always runs in constant time regardless of token length mismatch.
    /// </summary>
    private static byte[] PadOrTruncate(byte[] source, int targetLength)
    {
        if (source.Length == targetLength)
            return source;

        var result = new byte[targetLength];
        Buffer.BlockCopy(source, 0, result, 0, Math.Min(source.Length, targetLength));
        return result;
    }
}
