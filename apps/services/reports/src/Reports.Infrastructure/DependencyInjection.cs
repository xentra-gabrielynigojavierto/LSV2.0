using Amazon;
using Amazon.S3;
using LegalSynq.AuditClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Configuration;
using Reports.Contracts.Context;
using Reports.Contracts.Delivery;
using Reports.Contracts.Export;
using Reports.Contracts.Observability;
using Reports.Contracts.Persistence;
using Reports.Contracts.Queue;
using Reports.Contracts.Storage;
using Reports.Infrastructure.Adapters;
using Reports.Infrastructure.Exporters;
using Reports.Infrastructure.Observability;
using Reports.Infrastructure.Persistence;
using Reports.Infrastructure.Queue;

namespace Reports.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReportsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ReportsDb");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ReportsDbContext>(options =>
                options.UseMySql(
                    connectionString,
                    new MySqlServerVersion(new Version(8, 0, 0))));

            services.AddScoped<IReportRepository, EfReportRepository>();
            services.AddScoped<ITemplateRepository, EfTemplateRepository>();
            services.AddScoped<ITemplateAssignmentRepository, EfTemplateAssignmentRepository>();
            services.AddScoped<ITenantReportOverrideRepository, EfTenantReportOverrideRepository>();
            services.AddScoped<IReportScheduleRepository, EfReportScheduleRepository>();
            services.AddScoped<ITenantReportViewRepository, EfTenantReportViewRepository>();
        }
        else
        {
            services.AddSingleton<IReportRepository, MockReportRepository>();
            services.AddSingleton<ITemplateRepository, MockTemplateRepository>();
            services.AddSingleton<ITemplateAssignmentRepository, MockTemplateAssignmentRepository>();
            services.AddSingleton<ITenantReportOverrideRepository, MockTenantReportOverrideRepository>();
            services.AddSingleton<IReportScheduleRepository, MockReportScheduleRepository>();
            services.AddSingleton<ITenantReportViewRepository, MockTenantReportViewRepository>();
        }

        RegisterAuditAdapter(services, configuration);
        RegisterDataQueryAdapters(services, configuration);
        RegisterDeliveryAdapters(services, configuration);
        RegisterFileStorage(services, configuration);
        RegisterMetrics(services);

        RegisterIdentityAdapters(services, configuration);

        // Scoped: resolves JWT tenant/user identity for the current HTTP request
        services.AddScoped<ICurrentTenantContext, CurrentTenantContextAdapter>();

        services.AddSingleton<IDocumentAdapter, MockDocumentAdapter>();
        services.AddSingleton<INotificationAdapter, MockNotificationAdapter>();
        services.AddSingleton<IProductDataAdapter, MockProductDataAdapter>();

        services.AddSingleton<IJobQueue, InMemoryJobQueue>();
        services.AddSingleton<IJobProcessor, MockJobProcessor>();

        services.AddSingleton<IReportExporter, CsvReportExporter>();
        services.AddSingleton<IReportExporter, XlsxReportExporter>();
        services.AddSingleton<IReportExporter, PdfReportExporter>();

        return services;
    }

    private static void RegisterAuditAdapter(IServiceCollection services, IConfiguration configuration)
    {
        var auditEnabled = configuration.GetValue<bool>("AuditService:Enabled");
        var auditBaseUrl = configuration["AuditService:BaseUrl"];

        if (auditEnabled && !string.IsNullOrWhiteSpace(auditBaseUrl))
        {
            services.AddAuditEventClient(configuration.GetSection("AuditClient"), auditBaseUrl, configuration);
            services.AddSingleton<IAuditAdapter, SharedAuditAdapter>();
        }
        else
        {
            services.AddSingleton<IAuditAdapter, MockAuditAdapter>();
        }
    }

    private static void RegisterDataQueryAdapters(IServiceCollection services, IConfiguration configuration)
    {
        var liensSettings = configuration.GetSection(LiensDataSettings.SectionName).Get<LiensDataSettings>();
        var liensConnectionString = configuration.GetConnectionString("LiensDb");

        services.Configure<LiensDataSettings>(configuration.GetSection(LiensDataSettings.SectionName));

        if (liensSettings?.Enabled == true)
        {
            if (string.IsNullOrWhiteSpace(liensConnectionString))
                throw new InvalidOperationException("LiensData is enabled but ConnectionStrings:LiensDb is not configured.");
            services.AddSingleton<LiensReportDataQueryAdapter>();
            services.AddSingleton<MockReportDataQueryAdapter>();
            services.AddSingleton<IReportDataQueryAdapter>(sp =>
            {
                var adapters = new List<IReportDataQueryAdapter>
                {
                    sp.GetRequiredService<LiensReportDataQueryAdapter>(),
                    sp.GetRequiredService<MockReportDataQueryAdapter>(),
                };
                return new CompositeReportDataQueryAdapter(
                    adapters,
                    sp.GetRequiredService<ILogger<CompositeReportDataQueryAdapter>>());
            });
        }
        else
        {
            services.AddSingleton<IReportDataQueryAdapter, MockReportDataQueryAdapter>();
        }
    }

    private static void RegisterDeliveryAdapters(IServiceCollection services, IConfiguration configuration)
    {
        var emailSettings = configuration.GetSection(EmailDeliverySettings.SectionName).Get<EmailDeliverySettings>();
        var sftpSettings = configuration.GetSection(SftpDeliverySettings.SectionName).Get<SftpDeliverySettings>();

        services.Configure<EmailDeliverySettings>(configuration.GetSection(EmailDeliverySettings.SectionName));
        services.Configure<SftpDeliverySettings>(configuration.GetSection(SftpDeliverySettings.SectionName));

        services.AddSingleton<IReportDeliveryAdapter, OnScreenReportDeliveryAdapter>();

        if (emailSettings?.Enabled == true)
        {
            if (string.IsNullOrWhiteSpace(emailSettings.NotificationsBaseUrl))
                throw new InvalidOperationException("EmailDelivery is enabled but NotificationsBaseUrl is not configured.");

            services.AddHttpClient("EmailDelivery", client =>
            {
                client.BaseAddress = new Uri(emailSettings.NotificationsBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(emailSettings.TimeoutSeconds);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });
            services.AddSingleton<IReportDeliveryAdapter, HttpEmailReportDeliveryAdapter>();
        }
        else
        {
            services.AddSingleton<IReportDeliveryAdapter, EmailReportDeliveryAdapter>();
        }

        if (sftpSettings?.Enabled == true)
        {
            if (string.IsNullOrWhiteSpace(sftpSettings.Host))
                throw new InvalidOperationException("SftpDelivery is enabled but Host is not configured.");
            if (string.IsNullOrWhiteSpace(sftpSettings.Username))
                throw new InvalidOperationException("SftpDelivery is enabled but Username is not configured.");
            if (string.IsNullOrEmpty(sftpSettings.Password) && string.IsNullOrEmpty(sftpSettings.PrivateKeyPath))
                throw new InvalidOperationException("SftpDelivery is enabled but neither Password nor PrivateKeyPath is configured.");

            services.AddSingleton<IReportDeliveryAdapter, RealSftpReportDeliveryAdapter>();
        }
        else
        {
            services.AddSingleton<IReportDeliveryAdapter, SftpReportDeliveryAdapter>();
        }
    }

    private static void RegisterFileStorage(IServiceCollection services, IConfiguration configuration)
    {
        var storageSettings = configuration.GetSection(StorageSettings.SectionName).Get<StorageSettings>();
        services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));

        if (storageSettings?.Enabled == true)
        {
            if (string.IsNullOrWhiteSpace(storageSettings.BucketName))
                throw new InvalidOperationException("Storage is enabled but BucketName is not configured.");
            var s3Config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(storageSettings.Region),
            };

            if (!string.IsNullOrEmpty(storageSettings.AccessKeyId) && !string.IsNullOrEmpty(storageSettings.SecretAccessKey))
            {
                var credentials = new Amazon.Runtime.BasicAWSCredentials(storageSettings.AccessKeyId, storageSettings.SecretAccessKey);
                services.AddSingleton<IAmazonS3>(new AmazonS3Client(credentials, s3Config));
            }
            else
            {
                services.AddSingleton<IAmazonS3>(new AmazonS3Client(s3Config));
            }

            services.AddSingleton<IFileStorageAdapter, S3FileStorageAdapter>();
        }
        else
        {
            services.AddSingleton<IFileStorageAdapter, NullFileStorageAdapter>();
        }
    }

    private static void RegisterIdentityAdapters(IServiceCollection services, IConfiguration configuration)
    {
        var jwtSigningKey = configuration["Jwt:SigningKey"];
        var useRealIdentity = !string.IsNullOrWhiteSpace(jwtSigningKey);

        if (useRealIdentity)
        {
            services.AddScoped<IIdentityAdapter, ClaimsIdentityAdapter>();
            services.AddScoped<ITenantAdapter, ClaimsTenantAdapter>();
            services.AddScoped<IEntitlementAdapter, ClaimsEntitlementAdapter>();
        }
        else
        {
            services.AddSingleton<IIdentityAdapter, MockIdentityAdapter>();
            services.AddSingleton<ITenantAdapter, MockTenantAdapter>();
            services.AddSingleton<IEntitlementAdapter, MockEntitlementAdapter>();
        }
    }

    private static void RegisterMetrics(IServiceCollection services)
    {
        services.AddSingleton<IReportsMetrics, ReportsMetrics>();
    }

    private static void AddAuditEventClient(
        this IServiceCollection services,
        IConfigurationSection auditClientSection,
        string baseUrl,
        IConfiguration configuration)
    {
        var timeoutSeconds = configuration.GetValue<int?>("AuditService:TimeoutSeconds") ?? 5;
        var serviceToken = configuration["AuditService:ServiceToken"] ?? string.Empty;

        services.Configure<AuditClientOptions>(opts =>
        {
            opts.BaseUrl = baseUrl;
            opts.TimeoutSeconds = timeoutSeconds;
            opts.ServiceToken = serviceToken;
            opts.SourceSystem = "legalsynq-platform";
            opts.SourceService = "reports-service";
        });

        services.AddHttpClient<IAuditEventClient, HttpAuditEventClient>("AuditEventClient");
    }
}
