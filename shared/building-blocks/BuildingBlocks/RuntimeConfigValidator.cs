using Microsoft.Extensions.Configuration;

namespace BuildingBlocks;

/// <summary>
/// Lightweight startup configuration validator used by .NET microservices in non-Development
/// environments to fail fast when required secrets or URLs are missing, empty, or still set
/// to known placeholder values.
///
/// Design rules:
///   - Throws <see cref="InvalidOperationException"/> with a descriptive message.
///   - Never swallows errors — all paths either pass silently or throw immediately.
///   - No logging dependency — errors surface via the exception message in host output.
///   - Keep it simple: no overbuilt framework, no DI registration required.
///
/// Usage pattern (in Program.cs, before builder.Build()):
/// <code>
///   if (!builder.Environment.IsDevelopment())
///   {
///       var v = new RuntimeConfigValidator(builder.Configuration, "my-service");
///       v.RequireNonEmpty("Jwt:SigningKey");
///       v.RequireNotPlaceholder("Jwt:SigningKey");
///       v.RequireAbsoluteUrl("TenantService:BaseUrl");
///       v.RequireConnectionString("ConnectionStrings:MyDb");
///   }
/// </code>
/// </summary>
public sealed class RuntimeConfigValidator
{
    /// <summary>
    /// Known placeholder strings that must never appear in production configuration.
    /// Services store these in appsettings.json as documentation of required secrets.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultPlaceholders =
    [
        "REPLACE_VIA_SECRET",
        "CHANGE_ME",
        "YOUR_SECRET_HERE",
        "INSERT_SECRET_HERE",
        "TODO",
        "FIXME",
    ];

    private readonly IConfiguration _config;
    private readonly string         _serviceName;

    public RuntimeConfigValidator(IConfiguration config, string serviceName)
    {
        _config      = config      ?? throw new ArgumentNullException(nameof(config));
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
    }

    // ── Core checks ──────────────────────────────────────────────────────────

    /// <summary>
    /// Throws if the configuration value at <paramref name="key"/> is null, empty, or whitespace.
    /// </summary>
    public RuntimeConfigValidator RequireNonEmpty(string key)
    {
        var value = _config[key];
        if (string.IsNullOrWhiteSpace(value))
            Throw(key, "is missing or empty");
        return this;
    }

    /// <summary>
    /// Throws if the configuration value at <paramref name="key"/> contains any of the
    /// <see cref="DefaultPlaceholders"/> (case-insensitive substring match).
    /// The value is allowed to be empty/null — use <see cref="RequireNonEmpty"/> first if needed.
    /// </summary>
    public RuntimeConfigValidator RequireNotPlaceholder(string key)
        => RequireNotPlaceholder(key, DefaultPlaceholders);

    /// <summary>
    /// Throws if the configuration value at <paramref name="key"/> contains any of the
    /// supplied <paramref name="placeholders"/> (case-insensitive substring match).
    /// </summary>
    public RuntimeConfigValidator RequireNotPlaceholder(string key, IEnumerable<string> placeholders)
    {
        var value = _config[key];
        if (string.IsNullOrWhiteSpace(value))
            return this; // defer to RequireNonEmpty for the missing-value case
        foreach (var ph in placeholders)
        {
            if (value.Contains(ph, StringComparison.OrdinalIgnoreCase))
                Throw(key, $"contains placeholder value '{ph}'. Replace with a real secret.");
        }
        return this;
    }

    /// <summary>
    /// Throws if the configuration value at <paramref name="key"/> is not a well-formed
    /// absolute HTTP/HTTPS URL (empty string also fails).
    /// </summary>
    public RuntimeConfigValidator RequireAbsoluteUrl(string key)
    {
        var value = _config[key];
        if (string.IsNullOrWhiteSpace(value))
            Throw(key, "is missing or empty. Set an absolute HTTP/HTTPS URL.");
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
            Throw(key, $"is not a valid absolute HTTP/HTTPS URL. Current value: '{value}'");
        return this;
    }

    /// <summary>
    /// Throws if the connection string at <paramref name="key"/> is missing, empty, or
    /// still contains any of the <see cref="DefaultPlaceholders"/>.
    /// </summary>
    public RuntimeConfigValidator RequireConnectionString(string key)
    {
        RequireNonEmpty(key);
        RequireNotPlaceholder(key);
        return this;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void Throw(string key, string reason) =>
        throw new InvalidOperationException(
            $"[{_serviceName}] Configuration key '{key}' {reason}. " +
            $"Inject the correct value via environment variable using the " +
            $"ASP.NET Core double-underscore convention (e.g. {key.Replace(":", "__")}).");
}
