using Support.Api.Configuration;
using Support.Api.Data;
using Support.Api.Files;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Support.Tests;

/// <summary>
/// Test factory for the upload pipeline. Replaces the registered storage
/// provider with a <see cref="RecordingFileStorageProvider"/>, configures a
/// generous content-type allowlist, and lets tests inspect / control upload
/// behaviour without touching the disk or the network.
/// </summary>
public class FileUploadApiFactory : WebApplicationFactory<Program>
{
    public string DbName { get; } = $"support-tests-upload-{Guid.NewGuid()}";
    public RecordingFileStorageProvider Recorder { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Support:FileStorage:Mode"] = "Local", // any non-NoOp; the recorder replaces it
                ["Support:FileStorage:MaxFileSizeMb"] = "1",
                ["Support:FileStorage:LocalRootPath"] =
                    Path.Combine(Path.GetTempPath(), $"support-uploads-{Guid.NewGuid():N}"),
                ["Support:FileStorage:AllowedContentTypes:0"] = "application/pdf",
                ["Support:FileStorage:AllowedContentTypes:1"] = "image/png",
                ["Support:FileStorage:AllowedContentTypes:2"] = "text/plain",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<SupportDbContext>(o => o.UseInMemoryDatabase(DbName));

            // Replace whatever storage provider Program.cs registered with the recorder.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(ISupportFileStorageProvider))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);
            services.AddSingleton<ISupportFileStorageProvider>(Recorder);
        });
    }
}

/// <summary>
/// Exercises the real <see cref="LocalSupportFileStorageProvider"/> against a
/// per-test temp directory. Used to assert end-to-end behaviour of the Local
/// mode (file is actually written, doc id format, byte count) without
/// touching the production root path.
/// </summary>
public class LocalProviderApiFactory : WebApplicationFactory<Program>, IDisposable
{
    public string DbName { get; } = $"support-tests-local-{Guid.NewGuid()}";
    public string LocalRoot { get; } =
        Path.Combine(Path.GetTempPath(), $"support-uploads-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Support:FileStorage:Mode"] = "Local",
                ["Support:FileStorage:MaxFileSizeMb"] = "1",
                ["Support:FileStorage:LocalRootPath"] = LocalRoot,
                ["Support:FileStorage:AllowedContentTypes:0"] = "application/pdf",
                ["Support:FileStorage:AllowedContentTypes:1"] = "text/plain",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<SupportDbContext>(o => o.UseInMemoryDatabase(DbName));

            // Program.cs reads `Mode` at host-build time, BEFORE the
            // ConfigureAppConfiguration overrides above are applied — so it
            // ends up registering the NoOp provider. Force the real Local
            // provider here. The IOptionsMonitor<FileStorageOptions> binding
            // is deferred and DOES pick up our config overrides at use time,
            // so LocalRootPath will be the temp path we set above.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(ISupportFileStorageProvider))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);
            services.AddSingleton<ISupportFileStorageProvider, LocalSupportFileStorageProvider>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(LocalRoot))
        {
            try { Directory.Delete(LocalRoot, recursive: true); } catch { /* best-effort */ }
        }
    }
}
