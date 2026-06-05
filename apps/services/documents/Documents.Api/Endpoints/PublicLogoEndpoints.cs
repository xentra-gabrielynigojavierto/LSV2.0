using Amazon.S3;
using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Documents.Api.Endpoints;

public static class PublicLogoEndpoints
{
    private static readonly Guid TenantLogoDocTypeId =
        Guid.Parse("20000000-0000-0000-0000-000000000002");

    private static readonly HashSet<string> AllowedLogoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/tiff",
        "image/gif",
        "image/webp",
    };

    public static void MapPublicLogoEndpoints(this WebApplication app)
    {
        var requireCleanScan = app.Configuration.GetValue<bool>("Documents:RequireCleanScanForAccess", defaultValue: true);

        app.MapGet("/public/logo/{id:guid}", async (
            Guid id,
            DocsDbContext db,
            IStorageProvider storage,
            CancellationToken ct) =>
        {
            var doc = await db.Documents
                .AsNoTracking()
                .Where(d => d.Id == id
                         && !d.IsDeleted
                         && d.DocumentTypeId == TenantLogoDocTypeId
                         && d.IsPublishedAsLogo)
                .Select(d => new { d.StorageKey, d.StorageBucket, d.MimeType, d.ScanStatus })
                .FirstOrDefaultAsync(ct);

            if (doc is null || string.IsNullOrEmpty(doc.StorageKey))
                return Results.NotFound();

            // Block infected and failed documents unconditionally.
            // When RequireCleanScanForAccess is true (production) also block Pending and Skipped.
            if (doc.ScanStatus == ScanStatus.Infected || doc.ScanStatus == ScanStatus.Failed)
                return Results.NotFound();

            if (requireCleanScan && doc.ScanStatus != ScanStatus.Clean)
                return Results.NotFound();

            // Restrict public logo serving to image MIME types only
            if (string.IsNullOrEmpty(doc.MimeType) || !AllowedLogoMimeTypes.Contains(doc.MimeType))
                return Results.NotFound();

            try
            {
                var stream = await storage.DownloadAsync(doc.StorageKey, ct);
                return Results.Stream(stream, doc.MimeType);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Results.NotFound();
            }
        })
        .AllowAnonymous()
        .WithTags("Public")
        .WithSummary("Public logo access — image-only, clean-scan required, published-logo registration enforced")
        .ExcludeFromDescription();
    }
}
