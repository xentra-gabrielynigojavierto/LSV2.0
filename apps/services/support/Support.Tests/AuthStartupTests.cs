using FluentAssertions;
using Support.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Support.Tests;

/// <summary>
/// Verifies AuthExtensions.AddSupportAuth is fail-closed: in any non-Testing
/// environment, missing/insecure JWT configuration must throw at startup
/// rather than register a JWT pipeline that silently accepts attacker tokens.
/// </summary>
public class AuthStartupTests
{
    private sealed class FakeEnv : IWebHostEnvironment
    {
        public FakeEnv(string name) { EnvironmentName = name; }
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Support.Api";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static IConfiguration Cfg(Dictionary<string, string?> kv) =>
        new ConfigurationBuilder().AddInMemoryCollection(kv).Build();

    [Fact]
    public void Production_With_No_SigningKey_Throws()
    {
        var act = () => new ServiceCollection().AddSupportAuth(
            Cfg(new Dictionary<string, string?>()), new FakeEnv("Production"));
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Jwt:SigningKey*");
    }

    [Fact]
    public void Production_With_Short_SigningKey_Throws()
    {
        var act = () => new ServiceCollection().AddSupportAuth(Cfg(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "tooshort"
        }), new FakeEnv("Production"));
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*at least 32*");
    }

    [Fact]
    public void Production_With_Valid_Config_Succeeds()
    {
        var act = () => new ServiceCollection().AddSupportAuth(Cfg(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"]  = new string('k', 64),
            ["Jwt:Issuer"]     = "iss",
            ["Jwt:Audience"]   = "aud"
        }), new FakeEnv("Production"));
        act.Should().NotThrow();
    }

    [Fact]
    public void Production_With_Valid_Config_And_No_Issuer_Audience_Uses_Defaults()
    {
        // Issuer and Audience have safe defaults so omitting them should not throw.
        var act = () => new ServiceCollection().AddSupportAuth(Cfg(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = new string('k', 64)
        }), new FakeEnv("Production"));
        act.Should().NotThrow();
    }

    [Fact]
    public void Testing_Environment_Skips_Jwt_Validation()
    {
        // In Testing, AddSupportAuth registers TestAuthHandler instead of JWT,
        // so missing JWT config must not throw.
        var act = () => new ServiceCollection().AddSupportAuth(
            Cfg(new Dictionary<string, string?>()), new FakeEnv("Testing"));
        act.Should().NotThrow();
    }
}
