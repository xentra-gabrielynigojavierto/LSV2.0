namespace Comms.Application.Interfaces;

public record DocumentValidationResult(bool Exists, Guid? TenantId);

public interface IDocumentServiceClient
{
    Task<DocumentValidationResult> ValidateDocumentAsync(Guid documentId, Guid expectedTenantId, CancellationToken ct = default);
}
