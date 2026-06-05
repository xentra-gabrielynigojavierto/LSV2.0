using Support.Api.Data;
using Support.Api.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Support.Tests;

/// <summary>
/// Test factory that swaps the default NoOp notification publisher with a
/// <see cref="RecordingNotificationPublisher"/> so tests can assert
/// dispatched notifications.
/// </summary>
public class NotificationsApiFactory : WebApplicationFactory<Program>
{
    public string DbName { get; } = $"support-tests-notif-{Guid.NewGuid()}";
    public RecordingNotificationPublisher Recorder { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<SupportDbContext>(o => o.UseInMemoryDatabase(DbName));

            // Replace whatever publisher Program.cs registered with our recorder.
            var toRemove = services.Where(d => d.ServiceType == typeof(INotificationPublisher)).ToList();
            foreach (var d in toRemove) services.Remove(d);
            services.AddSingleton<INotificationPublisher>(Recorder);
        });
    }
}
