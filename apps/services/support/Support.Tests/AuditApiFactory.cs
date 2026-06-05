using Support.Api.Audit;
using Support.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Support.Tests;

/// <summary>
/// Test factory that swaps the default <see cref="IAuditPublisher"/>
/// with a <see cref="RecordingAuditPublisher"/> so audit dispatch
/// can be asserted. Notification publisher is left at its default
/// NoOp so notification side effects don't pollute audit assertions.
/// </summary>
public class AuditApiFactory : WebApplicationFactory<Program>
{
    public string DbName { get; } = $"support-tests-audit-{Guid.NewGuid()}";
    public RecordingAuditPublisher Recorder { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<SupportDbContext>(o => o.UseInMemoryDatabase(DbName));

            // Replace whatever audit publisher Program.cs registered with our recorder.
            var toRemove = services.Where(d => d.ServiceType == typeof(IAuditPublisher)).ToList();
            foreach (var d in toRemove) services.Remove(d);
            services.AddSingleton<IAuditPublisher>(Recorder);
        });
    }
}
