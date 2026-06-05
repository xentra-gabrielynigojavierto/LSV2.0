using System.Net.Http.Json;
using Identity.Application.Interfaces;
using Identity.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class CareConnectProvisioningHandler : IProductProvisioningHandler
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CareConnectProvisioningHandler> _logger;

    public string ProductCode => ProductCodes.SynqCareConnect;

    public CareConnectProvisioningHandler(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CareConnectProvisioningHandler> logger)
    {
        _httpClient = httpClientFactory.CreateClient("CareConnectInternal");
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ProductProvisioningHandlerResult> HandleAsync(
        ProductProvisioningContext context,
        CancellationToken ct = default)
    {
        int processed = 0;
        int created = 0;
        int linked = 0;
        var warnings = new List<string>();

        var providerOrgs = context.EligibleOrganizations
            .Where(o => string.Equals(o.OrgType, OrgType.Provider, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var org in providerOrgs)
        {
            processed++;
            try
            {
                var result = await ProvisionProviderForOrg(context.TenantId, org, ct);
                if (result == ProvisionOutcome.Created) created++;
                else if (result == ProvisionOutcome.Linked) linked++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CareConnect provider provisioning failed for org {OrgId} ({OrgName}). " +
                    "Provider may need manual activation.",
                    org.Id, org.DisplayName ?? org.Name);
                warnings.Add($"Org {org.Id} ({org.DisplayName ?? org.Name}): {ex.Message}");
            }
        }

        return new ProductProvisioningHandlerResult(
            ProductCode,
            processed,
            created,
            linked,
            warnings);
    }

    private async Task<ProvisionOutcome> ProvisionProviderForOrg(
        Guid tenantId, Organization org, CancellationToken ct)
    {
        var serviceToken = _configuration["CareConnect:InternalServiceToken"];
        if (string.IsNullOrEmpty(serviceToken))
            throw new InvalidOperationException(
                "CareConnect:InternalServiceToken is not configured. Cannot provision provider.");

        var request = new
        {
            tenantId,
            organizationId = org.Id,
            providerName = org.DisplayName ?? org.Name,
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/internal/provision-provider");
        httpRequest.Content = JsonContent.Create(request);
        httpRequest.Headers.Add("X-Internal-Service-Token", serviceToken);

        var response = await _httpClient.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "CareConnect internal provision returned {StatusCode} for org {OrgId}: {Body}",
                (int)response.StatusCode, org.Id, body);

            if ((int)response.StatusCode >= 500)
                throw new HttpRequestException($"CareConnect returned {(int)response.StatusCode}: {body}");

            return ProvisionOutcome.Skipped;
        }

        var result = await response.Content.ReadFromJsonAsync<InternalProvisionResponse>(ct);

        if (result is null)
            return ProvisionOutcome.Skipped;

        _logger.LogInformation(
            "CareConnect provider provisioned for org {OrgId}: ProviderId={ProviderId}, IsNew={IsNew}",
            org.Id, result.ProviderId, result.IsNew);

        return result.IsNew ? ProvisionOutcome.Created : ProvisionOutcome.Linked;
    }

    private enum ProvisionOutcome { Skipped, Created, Linked }

    private record InternalProvisionResponse(Guid ProviderId, bool IsNew);
}
