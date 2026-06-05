using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public class ResolvedTemplate
{
    public Template Template { get; set; } = null!;
    public TemplateVersion Version { get; set; } = null!;
}

public interface ITemplateResolutionService
{
    Task<ResolvedTemplate?> ResolveAsync(Guid tenantId, string templateKey, string channel);
    Task<ResolvedTemplate?> ResolveByProductAsync(Guid tenantId, string templateKey, string channel, string productType);
}
