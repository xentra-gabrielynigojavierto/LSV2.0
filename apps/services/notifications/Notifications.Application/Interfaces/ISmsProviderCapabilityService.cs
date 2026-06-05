using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-014: Registry of static SMS provider capability metadata.
/// Config-driven initially — does not call external provider APIs.
/// Twilio capability reflects existing full support.
/// Additional providers declare accurate partial capability.
/// </summary>
public interface ISmsProviderCapabilityService
{
    /// <summary>Returns capability metadata for the given provider type, or null if unknown.</summary>
    SmsProviderCapabilityDto? GetCapability(string providerType);

    /// <summary>Returns all registered provider capabilities.</summary>
    IReadOnlyList<SmsProviderCapabilityDto> GetAll();
}
