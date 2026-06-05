using BuildingBlocks;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Tests;

public class RuntimeConfigValidatorTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static RuntimeConfigValidator Validator(params (string key, string? value)[] pairs)
    {
        var dict = pairs.ToDictionary(p => p.key, p => p.value);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(dict!)
            .Build();
        return new RuntimeConfigValidator(config, "test-service");
    }

    // ── RequireNonEmpty ───────────────────────────────────────────────────────

    [Fact]
    public void RequireNonEmpty_PassesWhenValueIsPresent()
    {
        var v = Validator(("Jwt:SigningKey", "some-real-key-here-1234567890"));
        v.RequireNonEmpty("Jwt:SigningKey"); // no throw
    }

    [Fact]
    public void RequireNonEmpty_ThrowsWhenKeyIsMissing()
    {
        var v = Validator();
        var ex = Assert.Throws<InvalidOperationException>(() => v.RequireNonEmpty("Jwt:SigningKey"));
        Assert.Contains("Jwt:SigningKey", ex.Message);
        Assert.Contains("missing or empty", ex.Message);
        Assert.Contains("test-service", ex.Message);
    }

    [Fact]
    public void RequireNonEmpty_ThrowsWhenValueIsEmpty()
    {
        var v = Validator(("Jwt:SigningKey", ""));
        Assert.Throws<InvalidOperationException>(() => v.RequireNonEmpty("Jwt:SigningKey"));
    }

    [Fact]
    public void RequireNonEmpty_ThrowsWhenValueIsWhitespace()
    {
        var v = Validator(("Jwt:SigningKey", "   "));
        Assert.Throws<InvalidOperationException>(() => v.RequireNonEmpty("Jwt:SigningKey"));
    }

    // ── RequireNotPlaceholder ─────────────────────────────────────────────────

    [Fact]
    public void RequireNotPlaceholder_PassesWhenValueIsReal()
    {
        var v = Validator(("Jwt:SigningKey", "prod-signing-key-abc-XYZ-1234567890"));
        v.RequireNotPlaceholder("Jwt:SigningKey"); // no throw
    }

    [Fact]
    public void RequireNotPlaceholder_PassesWhenValueIsEmpty_DeferToRequireNonEmpty()
    {
        // Empty values are allowed through — RequireNonEmpty is responsible for that gate
        var v = Validator(("Jwt:SigningKey", ""));
        v.RequireNotPlaceholder("Jwt:SigningKey"); // must not throw
    }

    [Theory]
    [InlineData("REPLACE_VIA_SECRET")]
    [InlineData("replace_via_secret")]            // case-insensitive
    [InlineData("REPLACE_VIA_SECRET_minimum_32")] // prefix match
    [InlineData("CHANGE_ME")]
    [InlineData("YOUR_SECRET_HERE")]
    [InlineData("INSERT_SECRET_HERE")]
    [InlineData("TODO")]
    [InlineData("FIXME")]
    public void RequireNotPlaceholder_ThrowsForKnownPlaceholders(string placeholder)
    {
        var v = Validator(("Jwt:SigningKey", placeholder));
        var ex = Assert.Throws<InvalidOperationException>(
            () => v.RequireNotPlaceholder("Jwt:SigningKey"));
        Assert.Contains("Jwt:SigningKey", ex.Message);
        Assert.Contains("placeholder", ex.Message);
    }

    [Fact]
    public void RequireNotPlaceholder_ThrowsWhenPlaceholderEmbeddedInConnectionString()
    {
        var connStr = "server=db.example.com;database=mydb;password=REPLACE_VIA_SECRET";
        var v = Validator(("ConnectionStrings:MyDb", connStr));
        Assert.Throws<InvalidOperationException>(
            () => v.RequireNotPlaceholder("ConnectionStrings:MyDb"));
    }

    // ── RequireAbsoluteUrl ────────────────────────────────────────────────────

    [Theory]
    [InlineData("http://localhost:5005")]
    [InlineData("https://tenant.prod.legalsynq.com")]
    [InlineData("http://10.0.0.1:5005")]
    public void RequireAbsoluteUrl_PassesForValidUrls(string url)
    {
        var v = Validator(("TenantService:BaseUrl", url));
        v.RequireAbsoluteUrl("TenantService:BaseUrl"); // no throw
    }

    [Fact]
    public void RequireAbsoluteUrl_ThrowsWhenEmpty()
    {
        var v = Validator(("TenantService:BaseUrl", ""));
        var ex = Assert.Throws<InvalidOperationException>(
            () => v.RequireAbsoluteUrl("TenantService:BaseUrl"));
        Assert.Contains("TenantService:BaseUrl", ex.Message);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("//example.com")]
    [InlineData("relative/path")]
    public void RequireAbsoluteUrl_ThrowsForInvalidUrls(string url)
    {
        var v = Validator(("TenantService:BaseUrl", url));
        Assert.Throws<InvalidOperationException>(
            () => v.RequireAbsoluteUrl("TenantService:BaseUrl"));
    }

    // ── RequireConnectionString ───────────────────────────────────────────────

    [Fact]
    public void RequireConnectionString_PassesForRealConnectionString()
    {
        const string cs = "server=db.prod.com;port=3306;database=mydb;user=svc;password=realSecret123!";
        var v = Validator(("ConnectionStrings:MyDb", cs));
        v.RequireConnectionString("ConnectionStrings:MyDb"); // no throw
    }

    [Fact]
    public void RequireConnectionString_ThrowsWhenEmpty()
    {
        var v = Validator(("ConnectionStrings:MyDb", ""));
        Assert.Throws<InvalidOperationException>(
            () => v.RequireConnectionString("ConnectionStrings:MyDb"));
    }

    [Fact]
    public void RequireConnectionString_ThrowsWhenContainsPlaceholder()
    {
        const string cs = "server=db.prod.com;password=REPLACE_VIA_SECRET";
        var v = Validator(("ConnectionStrings:MyDb", cs));
        Assert.Throws<InvalidOperationException>(
            () => v.RequireConnectionString("ConnectionStrings:MyDb"));
    }

    // ── Chaining ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validator_SupportsFluentChaining()
    {
        var v = Validator(
            ("Jwt:SigningKey", "real-production-signing-key-abc123"),
            ("TenantService:BaseUrl", "https://tenant.legalsynq.com"),
            ("ConnectionStrings:MyDb", "server=db;password=realpass"));

        v.RequireNonEmpty("Jwt:SigningKey")
         .RequireNotPlaceholder("Jwt:SigningKey")
         .RequireAbsoluteUrl("TenantService:BaseUrl")
         .RequireConnectionString("ConnectionStrings:MyDb"); // no throw
    }

    // ── Error message quality ─────────────────────────────────────────────────

    [Fact]
    public void ErrorMessage_IncludesEnvVarConvention()
    {
        var v = Validator();
        var ex = Assert.Throws<InvalidOperationException>(
            () => v.RequireNonEmpty("Jwt:SigningKey"));
        Assert.Contains("Jwt__SigningKey", ex.Message); // double-underscore convention
    }

    [Fact]
    public void ErrorMessage_IncludesServiceName()
    {
        var v = Validator();
        var ex = Assert.Throws<InvalidOperationException>(
            () => v.RequireNonEmpty("Jwt:SigningKey"));
        Assert.Contains("test-service", ex.Message);
    }
}
