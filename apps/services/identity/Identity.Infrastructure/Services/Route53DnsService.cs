using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using Identity.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

public sealed class Route53DnsOptions
{
    public string HostedZoneId { get; set; } = string.Empty;
    public string BaseDomain { get; set; } = string.Empty;
    public string RecordType { get; set; } = "A";
    public string RecordValue { get; set; } = string.Empty;
    public string? TxtVerificationValue { get; set; }
    public long Ttl { get; set; } = 300;
    public string Region { get; set; } = "us-east-2";
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
}

public sealed class Route53DnsService : IDnsService, IDisposable
{
    private readonly IAmazonRoute53 _route53;
    private readonly Route53DnsOptions _opts;
    private readonly ILogger<Route53DnsService> _log;

    public string BaseDomain => _opts.BaseDomain;

    public Route53DnsService(IOptions<Route53DnsOptions> opts, ILogger<Route53DnsService> log)
    {
        _opts = opts.Value;
        _log = log;

        var config = new AmazonRoute53Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_opts.Region)
        };

        _route53 = _opts.AccessKeyId is not null
            ? new AmazonRoute53Client(_opts.AccessKeyId, _opts.SecretAccessKey, config)
            : new AmazonRoute53Client(config);
    }

    public async Task<bool> CreateSubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        var fqdn = $"{subdomain.ToLowerInvariant()}.{_opts.BaseDomain}";
        await DeleteConflictingRecordsAsync(fqdn, ct);

        var aSuccess = await UpsertRecordAsync(fqdn, ChangeAction.UPSERT, ct);

        if (aSuccess && !string.IsNullOrWhiteSpace(_opts.TxtVerificationValue))
        {
            var txtSuccess = await UpsertTxtRecordAsync(fqdn, ChangeAction.UPSERT, ct);
            if (!txtSuccess)
                _log.LogWarning("A record created but TXT verification record failed for {Fqdn}", fqdn);
        }

        return aSuccess;
    }

    public async Task<bool> DeleteSubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        var fqdn = $"{subdomain.ToLowerInvariant()}.{_opts.BaseDomain}";
        var deleted = await UpsertRecordAsync(fqdn, ChangeAction.DELETE, ct);

        if (!string.IsNullOrWhiteSpace(_opts.TxtVerificationValue))
            await UpsertTxtRecordAsync(fqdn, ChangeAction.DELETE, ct);

        return deleted;
    }

    private async Task DeleteConflictingRecordsAsync(string fqdn, CancellationToken ct)
    {
        var targetType = RRType.FindValue(_opts.RecordType);
        var conflictTypes = new[] { RRType.CNAME, RRType.A, RRType.AAAA }
            .Where(t => t != targetType)
            .ToList();

        try
        {
            var listRequest = new ListResourceRecordSetsRequest
            {
                HostedZoneId = _opts.HostedZoneId,
                StartRecordName = fqdn,
                MaxItems = "10"
            };

            var listResponse = await _route53.ListResourceRecordSetsAsync(listRequest, ct);
            var changes = new List<Change>();

            foreach (var rrs in listResponse.ResourceRecordSets)
            {
                if (!string.Equals(rrs.Name.TrimEnd('.'), fqdn.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (conflictTypes.Contains(rrs.Type))
                {
                    _log.LogInformation("Deleting conflicting {Type} record for {Fqdn}", rrs.Type.Value, fqdn);
                    changes.Add(new Change
                    {
                        Action = ChangeAction.DELETE,
                        ResourceRecordSet = rrs
                    });
                }
            }

            if (changes.Count > 0)
            {
                var deleteRequest = new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = _opts.HostedZoneId,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = changes,
                        Comment = $"LegalSynq: removing conflicting records before creating {targetType.Value} for {fqdn}"
                    }
                };
                await _route53.ChangeResourceRecordSetsAsync(deleteRequest, ct);
                _log.LogInformation("Deleted {Count} conflicting record(s) for {Fqdn}", changes.Count, fqdn);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not check/delete conflicting records for {Fqdn} — proceeding with upsert", fqdn);
        }
    }

    private async Task<bool> UpsertRecordAsync(string fqdn, ChangeAction action, CancellationToken ct)
    {
        try
        {
            var change = new Change
            {
                Action = action,
                ResourceRecordSet = new ResourceRecordSet
                {
                    Name = fqdn,
                    Type = RRType.FindValue(_opts.RecordType),
                    TTL = _opts.Ttl,
                    ResourceRecords = new List<ResourceRecord>
                    {
                        new ResourceRecord { Value = _opts.RecordValue }
                    }
                }
            };

            var request = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = _opts.HostedZoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change> { change },
                    Comment = $"LegalSynq tenant subdomain: {action.Value} {fqdn}"
                }
            };

            var response = await _route53.ChangeResourceRecordSetsAsync(request, ct);
            _log.LogInformation(
                "Route53 {Action} {Type} for {Fqdn}: status={Status}, changeId={ChangeId}",
                action.Value, _opts.RecordType, fqdn, response.ChangeInfo.Status.Value, response.ChangeInfo.Id);

            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Route53 {Action} failed for {Fqdn}", action.Value, fqdn);
            return false;
        }
    }

    private async Task<bool> UpsertTxtRecordAsync(string fqdn, ChangeAction action, CancellationToken ct)
    {
        try
        {
            var txtValue = $"\"{_opts.TxtVerificationValue}\"";
            var change = new Change
            {
                Action = action,
                ResourceRecordSet = new ResourceRecordSet
                {
                    Name = fqdn,
                    Type = RRType.TXT,
                    TTL = _opts.Ttl,
                    ResourceRecords = new List<ResourceRecord>
                    {
                        new ResourceRecord { Value = txtValue }
                    }
                }
            };

            var request = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = _opts.HostedZoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change> { change },
                    Comment = $"LegalSynq tenant TXT verification: {action.Value} {fqdn}"
                }
            };

            var response = await _route53.ChangeResourceRecordSetsAsync(request, ct);
            _log.LogInformation(
                "Route53 {Action} TXT for {Fqdn}: status={Status}, changeId={ChangeId}",
                action.Value, fqdn, response.ChangeInfo.Status.Value, response.ChangeInfo.Id);

            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Route53 TXT {Action} failed for {Fqdn}", action.Value, fqdn);
            return false;
        }
    }

    public void Dispose()
    {
        _route53.Dispose();
    }
}
