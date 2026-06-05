using Documents.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Documents.Infrastructure.Storage;

public static class StorageProviderFactory
{
    public static IStorageProvider Create(string providerName, IServiceProvider services)
    {
        return providerName.ToLowerInvariant() switch
        {
            "database" => services.GetRequiredService<DatabaseStorageProvider>(),
            "s3"       => services.GetRequiredService<S3StorageProvider>(),
            "local"    => services.GetRequiredService<LocalStorageProvider>(),
            _          => services.GetRequiredService<LocalStorageProvider>(),
        };
    }
}
