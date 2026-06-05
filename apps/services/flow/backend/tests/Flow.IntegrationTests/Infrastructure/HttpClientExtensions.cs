using System.Net.Http.Json;

namespace Flow.IntegrationTests.Infrastructure;

/// <summary>
/// Helpers that build an <see cref="HttpClient"/> stamped with the
/// TestAuth headers for a particular caller persona, and small wrappers
/// around the atomic execution endpoints under test.
/// </summary>
public static class HttpClientExtensions
{
    public static HttpClient AsUser(
        this FlowApiFactory factory,
        string tenantId,
        string sub = "user-1",
        string? role = null,
        string? permissions = null,
        string? productRoles = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthDefaults.SubHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthDefaults.TenantHeader, tenantId);
        if (!string.IsNullOrWhiteSpace(role))
            client.DefaultRequestHeaders.Add(TestAuthDefaults.RoleHeader, role);
        if (!string.IsNullOrWhiteSpace(permissions))
            client.DefaultRequestHeaders.Add(TestAuthDefaults.PermissionsHeader, permissions);
        if (!string.IsNullOrWhiteSpace(productRoles))
            client.DefaultRequestHeaders.Add(TestAuthDefaults.ProductRolesHeader, productRoles);
        return client;
    }

    /// <summary>Service-token shaped caller (sub starts with <c>service:</c>).</summary>
    public static HttpClient AsService(
        this FlowApiFactory factory,
        string tenantId,
        string serviceName = "liens-api",
        string? actor = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthDefaults.SubHeader, "service:" + serviceName);
        client.DefaultRequestHeaders.Add(TestAuthDefaults.TenantHeader, tenantId);
        client.DefaultRequestHeaders.Add(TestAuthDefaults.AudHeader, "ls.flow");
        if (!string.IsNullOrWhiteSpace(actor))
            client.DefaultRequestHeaders.Add(TestAuthDefaults.ActorHeader, actor);
        return client;
    }

    public static HttpClient Anonymous(this FlowApiFactory factory) => factory.CreateClient();

    public static string Path(string slug, string entityType, string entityId, Guid instanceId, string? suffix = null)
        => $"/api/v1/product-workflows/{slug}/{entityType}/{entityId}/{instanceId}" +
           (string.IsNullOrEmpty(suffix) ? "" : $"/{suffix}");

    public static Task<HttpResponseMessage> AdvanceAsync(this HttpClient client, string slug, string entityType,
        string entityId, Guid instanceId, string expectedCurrentStepKey, string? toStepKey = null)
    {
        return client.PostAsJsonAsync(
            Path(slug, entityType, entityId, instanceId, "advance"),
            new { ExpectedCurrentStepKey = expectedCurrentStepKey, ToStepKey = toStepKey });
    }

    public static Task<HttpResponseMessage> CompleteAsync(this HttpClient client, string slug, string entityType,
        string entityId, Guid instanceId)
    {
        return client.PostAsync(Path(slug, entityType, entityId, instanceId, "complete"), content: null);
    }
}
