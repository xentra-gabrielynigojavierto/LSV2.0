using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Authorization;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

public class PolicyEvaluationService : IPolicyEvaluationService
{
    private readonly IdentityDbContext _db;
    private readonly IAttributeProvider _attributeProvider;
    private readonly ILogger<PolicyEvaluationService> _logger;
    private readonly IPolicyEvaluationCache _cache;
    private readonly IPolicyVersionProvider _versionProvider;
    private readonly PolicyCachingOptions _cachingOptions;
    private readonly PolicyLoggingOptions _loggingOptions;
    private readonly PolicyVersioningOptions _versioningOptions;
    private readonly PolicyMetrics _metrics;

    private static readonly ThreadLocal<Random> SamplingRng = new(() => new Random());

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();
    private static readonly ConcurrentDictionary<string, (PolicyEvaluationResult Result, DateTime ExpiresAt)> _inflightResults = new();

    public PolicyEvaluationService(
        IdentityDbContext db,
        IAttributeProvider attributeProvider,
        ILogger<PolicyEvaluationService> logger,
        IPolicyEvaluationCache cache,
        IPolicyVersionProvider versionProvider,
        IOptions<PolicyCachingOptions> cachingOptions,
        IOptions<PolicyLoggingOptions> loggingOptions,
        IOptions<PolicyVersioningOptions> versioningOptions,
        PolicyMetrics metrics)
    {
        _db = db;
        _attributeProvider = attributeProvider;
        _logger = logger;
        _cache = cache;
        _versionProvider = versionProvider;
        _cachingOptions = cachingOptions.Value;
        _loggingOptions = loggingOptions.Value;
        _versioningOptions = versioningOptions.Value;
        _metrics = metrics;
    }

    public async Task<PolicyEvaluationResult> EvaluateAsync(
        ClaimsPrincipal user,
        string permissionCode,
        Dictionary<string, object?>? resourceContext = null,
        HttpContext? httpContext = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var userId = user.FindFirst("sub")?.Value ?? "";
        var tenantId = user.FindFirst("tenant_id")?.Value ?? "";
        var accessVersion = user.FindFirst("access_version")?.Value ?? "";
        var endpoint = httpContext?.Request.Path.Value ?? "";
        var resourceContextPresent = resourceContext != null && resourceContext.Count > 0;

        var useTenantScope = string.Equals(_versioningOptions.Scope, "Tenant", StringComparison.OrdinalIgnoreCase);

        var versionSw = Stopwatch.StartNew();
        long policyVersion;
        if (useTenantScope)
        {
            var globalVersion = _versionProvider.CurrentVersion;
            var tenantVersion = _versionProvider.GetVersion(tenantId);
            policyVersion = Math.Max(globalVersion, tenantVersion);
        }
        else
        {
            policyVersion = _versionProvider.CurrentVersion;
        }
        versionSw.Stop();
        _metrics.RecordVersionRead(versionSw.ElapsedMilliseconds);

        var cacheKey = BuildCacheKey(_cachingOptions.KeyPrefix, tenantId, userId, permissionCode, policyVersion, resourceContext);

        var cachingEnabled = _cachingOptions.Enabled && resourceContextPresent;
        var cacheWriteAllowed = cachingEnabled && !_versionProvider.IsFrozen;

        if (cachingEnabled)
        {
            var cacheSw = Stopwatch.StartNew();
            var cached = await _cache.GetAsync(cacheKey, ct);
            cacheSw.Stop();

            if (cached != null)
            {
                _metrics.RecordCacheHit(cacheSw.ElapsedMilliseconds);
                sw.Stop();
                _metrics.RecordEvaluation(sw.ElapsedMilliseconds);

                return new PolicyEvaluationResult
                {
                    Allowed = cached.Allowed,
                    Reason = cached.Reason,
                    MatchedPolicies = cached.MatchedPolicies.Select(mp => new MatchedPolicy
                    {
                        PolicyCode = mp.PolicyCode,
                        PolicyName = mp.PolicyName,
                        Effect = mp.Effect,
                        Priority = mp.Priority,
                        EvaluationOrder = mp.EvaluationOrder,
                        Passed = mp.Passed,
                        Reason = mp.Reason,
                        RuleResults = mp.RuleResults.Select(rr => new RuleResult
                        {
                            Field = rr.Field,
                            Operator = rr.Operator,
                            ExpectedValue = rr.ExpectedValue,
                            ActualValue = rr.ActualValue,
                            Passed = rr.Passed,
                        }).ToList(),
                    }).ToList(),
                    DenyOverrideApplied = cached.DenyOverrideApplied,
                    DenyOverridePolicyCode = cached.DenyOverridePolicyCode,
                    PolicyVersion = cached.PolicyVersion,
                    ResourceContextPresent = cached.ResourceContextPresent,
                    CacheHit = true,
                    EvaluationElapsedMs = sw.ElapsedMilliseconds,
                };
            }

            _metrics.RecordCacheMiss(cacheSw.ElapsedMilliseconds);
        }

        if (cachingEnabled)
        {
            return await EvaluateWithStampedeProtection(
                cacheKey, user, permissionCode, resourceContext, httpContext,
                userId, tenantId, accessVersion, endpoint, resourceContextPresent,
                policyVersion, cacheWriteAllowed, sw, ct);
        }

        return await EvaluateCore(
            user, permissionCode, resourceContext, httpContext,
            userId, tenantId, accessVersion, endpoint, resourceContextPresent,
            policyVersion, cacheKey, cacheWriteAllowed, sw, ct);
    }

    private async Task<PolicyEvaluationResult> EvaluateWithStampedeProtection(
        string cacheKey, ClaimsPrincipal user, string permissionCode,
        Dictionary<string, object?>? resourceContext, HttpContext? httpContext,
        string userId, string tenantId, string accessVersion, string endpoint,
        bool resourceContextPresent, long policyVersion, bool cacheWriteAllowed,
        Stopwatch sw, CancellationToken ct)
    {
        var keyLock = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

        if (!await keyLock.WaitAsync(TimeSpan.FromSeconds(5), ct))
        {
            return await EvaluateCore(
                user, permissionCode, resourceContext, httpContext,
                userId, tenantId, accessVersion, endpoint, resourceContextPresent,
                policyVersion, cacheKey, cacheWriteAllowed, sw, ct);
        }

        try
        {
            if (_inflightResults.TryGetValue(cacheKey, out var inflight) && inflight.ExpiresAt > DateTime.UtcNow)
            {
                _metrics.RecordStampedeCoalesced();
                sw.Stop();
                _metrics.RecordEvaluation(sw.ElapsedMilliseconds);

                return new PolicyEvaluationResult
                {
                    Allowed = inflight.Result.Allowed,
                    Reason = inflight.Result.Reason,
                    MatchedPolicies = inflight.Result.MatchedPolicies.Select(mp => new MatchedPolicy
                    {
                        PolicyCode = mp.PolicyCode,
                        PolicyName = mp.PolicyName,
                        Effect = mp.Effect,
                        Priority = mp.Priority,
                        EvaluationOrder = mp.EvaluationOrder,
                        Passed = mp.Passed,
                        Reason = mp.Reason,
                        RuleResults = mp.RuleResults.Select(rr => new RuleResult
                        {
                            Field = rr.Field,
                            Operator = rr.Operator,
                            ExpectedValue = rr.ExpectedValue,
                            ActualValue = rr.ActualValue,
                            Passed = rr.Passed,
                        }).ToList(),
                    }).ToList(),
                    DenyOverrideApplied = inflight.Result.DenyOverrideApplied,
                    DenyOverridePolicyCode = inflight.Result.DenyOverridePolicyCode,
                    PolicyVersion = inflight.Result.PolicyVersion,
                    ResourceContextPresent = inflight.Result.ResourceContextPresent,
                    CacheHit = true,
                    EvaluationElapsedMs = sw.ElapsedMilliseconds,
                };
            }

            var result = await EvaluateCore(
                user, permissionCode, resourceContext, httpContext,
                userId, tenantId, accessVersion, endpoint, resourceContextPresent,
                policyVersion, cacheKey, cacheWriteAllowed, sw, ct);

            var ttlSeconds = _cachingOptions.TtlSeconds > 0 ? _cachingOptions.TtlSeconds : 60;
            _inflightResults[cacheKey] = (result, DateTime.UtcNow.AddSeconds(Math.Min(ttlSeconds, 5)));

            _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(t =>
            {
                _inflightResults.TryRemove(cacheKey, out var _removed);
                if (_keyLocks.TryGetValue(cacheKey, out var lk) && lk.CurrentCount > 0)
                    _keyLocks.TryRemove(cacheKey, out var _removedLock);
            });

            return result;
        }
        finally
        {
            keyLock.Release();
        }
    }

    private async Task<PolicyEvaluationResult> EvaluateCore(
        ClaimsPrincipal user, string permissionCode,
        Dictionary<string, object?>? resourceContext, HttpContext? httpContext,
        string userId, string tenantId, string accessVersion, string endpoint,
        bool resourceContextPresent, long policyVersion, string cacheKey,
        bool cacheWriteAllowed, Stopwatch sw, CancellationToken ct)
    {
        var permissionPolicies = await _db.PermissionPolicies
            .Where(pp => pp.PermissionCode == permissionCode && pp.IsActive)
            .Select(pp => pp.PolicyId)
            .ToListAsync(ct);

        if (permissionPolicies.Count == 0)
        {
            var noPoliciesResult = PolicyEvaluationResult.Allow("No policies attached to permission");
            noPoliciesResult.EvaluationElapsedMs = sw.ElapsedMilliseconds;
            noPoliciesResult.PolicyVersion = policyVersion;
            noPoliciesResult.ResourceContextPresent = resourceContextPresent;
            sw.Stop();
            _metrics.RecordEvaluation(sw.ElapsedMilliseconds);
            return noPoliciesResult;
        }

        var policies = await _db.Policies
            .Where(p => permissionPolicies.Contains(p.Id) && p.IsActive)
            .Include(p => p.Rules)
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.PolicyCode)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

        if (policies.Count == 0)
        {
            var noActiveResult = PolicyEvaluationResult.Allow("No active policies found");
            noActiveResult.EvaluationElapsedMs = sw.ElapsedMilliseconds;
            noActiveResult.PolicyVersion = policyVersion;
            noActiveResult.ResourceContextPresent = resourceContextPresent;
            sw.Stop();
            _metrics.RecordEvaluation(sw.ElapsedMilliseconds);
            return noActiveResult;
        }

        var userAttrs = await _attributeProvider.GetUserAttributesAsync(user, ct);
        var resourceAttrs = await _attributeProvider.GetResourceAttributesAsync(resourceContext, ct);

        Dictionary<string, object?> requestAttrs;
        if (httpContext != null)
            requestAttrs = await _attributeProvider.GetRequestContextAsync(httpContext, ct);
        else
            requestAttrs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var allAttributes = MergeAttributes(userAttrs, resourceAttrs, requestAttrs);

        var matchedPolicies = new List<MatchedPolicy>();
        MatchedPolicy? firstDenyOverride = null;

        for (var i = 0; i < policies.Count; i++)
        {
            var policy = policies[i];
            var policyResult = EvaluatePolicy(policy, allAttributes, i);
            matchedPolicies.Add(policyResult);

            LogPolicyDecision(userId, tenantId, endpoint, permissionCode, policy, policyResult, resourceContextPresent, accessVersion, sw.ElapsedMilliseconds);

            if (policy.Effect == PolicyEffect.Deny && policyResult.Passed && firstDenyOverride == null)
            {
                firstDenyOverride = policyResult;
            }
        }

        sw.Stop();

        PolicyEvaluationResult result;

        if (firstDenyOverride != null)
        {
            result = PolicyEvaluationResult.DenyWithOverride(
                $"Deny policy '{firstDenyOverride.PolicyCode}' matched — deny override applied",
                firstDenyOverride.PolicyCode,
                matchedPolicies);
        }
        else
        {
            var allowPolicies = matchedPolicies.Where(mp => mp.Effect == "Allow").ToList();
            var allAllowsPassed = allowPolicies.Count == 0 || allowPolicies.All(p => p.Passed);

            if (allAllowsPassed)
            {
                result = PolicyEvaluationResult.AllowWithPolicies(
                    $"All {policies.Count} policies evaluated — access granted",
                    matchedPolicies);
            }
            else
            {
                var failedAllows = allowPolicies.Where(p => !p.Passed).ToList();
                result = PolicyEvaluationResult.Deny(
                    $"Failed {failedAllows.Count} allow polic{(failedAllows.Count != 1 ? "ies" : "y")}: {string.Join(", ", failedAllows.Select(p => p.PolicyCode))}",
                    matchedPolicies);
            }
        }

        result.EvaluationElapsedMs = sw.ElapsedMilliseconds;
        result.PolicyVersion = policyVersion;
        result.ResourceContextPresent = resourceContextPresent;

        _metrics.RecordEvaluation(sw.ElapsedMilliseconds);

        if (cacheWriteAllowed)
        {
            var ttl = TimeSpan.FromSeconds(_cachingOptions.TtlSeconds > 0 ? _cachingOptions.TtlSeconds : 60);
            await _cache.SetAsync(cacheKey, result, ttl, ct);
        }

        return result;
    }

    private void LogPolicyDecision(
        string userId, string tenantId, string endpoint, string permission,
        Policy policy, MatchedPolicy mp, bool resourceContextPresent,
        string accessVersion, long elapsedMs)
    {
        if (!_loggingOptions.Enabled)
            return;

        var effect = policy.Effect == PolicyEffect.Deny && mp.Passed ? "Deny" : mp.Passed ? "Allow" : "Deny";
        var resultStr = mp.Passed && policy.Effect != PolicyEffect.Deny ? "ALLOW" : "DENY";

        if (resultStr == "ALLOW" && _loggingOptions.SampleRate < 1.0 && SamplingRng.Value!.NextDouble() > _loggingOptions.SampleRate)
            return;

        if (resultStr == "DENY")
        {
            var level = ParseLogLevel(_loggingOptions.DenyLevel, LogLevel.Warning);
            _logger.Log(level,
                "PolicyDecision: event=PolicyDecision userId={UserId} tenantId={TenantId} endpoint={Endpoint} permission={Permission} policyCode={PolicyCode} policyId={PolicyId} effect={Effect} result={Result} resourceContextPresent={ResourceContextPresent} accessVersion={AccessVersion} evaluationElapsedMs={ElapsedMs} ruleResults={RuleResults}",
                userId, tenantId, endpoint, permission, policy.PolicyCode, policy.Id,
                effect, resultStr, resourceContextPresent, accessVersion, elapsedMs,
                SerializeRuleResults(mp.RuleResults));
        }
        else
        {
            var level = ParseLogLevel(_loggingOptions.AllowLevel, LogLevel.Debug);
            if (_loggingOptions.LogRuleResultsOnAllow)
            {
                _logger.Log(level,
                    "PolicyDecision: event=PolicyDecision userId={UserId} tenantId={TenantId} endpoint={Endpoint} permission={Permission} policyCode={PolicyCode} policyId={PolicyId} effect={Effect} result={Result} resourceContextPresent={ResourceContextPresent} accessVersion={AccessVersion} evaluationElapsedMs={ElapsedMs} ruleResults={RuleResults}",
                    userId, tenantId, endpoint, permission, policy.PolicyCode, policy.Id,
                    effect, resultStr, resourceContextPresent, accessVersion, elapsedMs,
                    SerializeRuleResults(mp.RuleResults));
            }
            else
            {
                _logger.Log(level,
                    "PolicyDecision: event=PolicyDecision userId={UserId} tenantId={TenantId} endpoint={Endpoint} permission={Permission} policyCode={PolicyCode} policyId={PolicyId} effect={Effect} result={Result} resourceContextPresent={ResourceContextPresent} accessVersion={AccessVersion} evaluationElapsedMs={ElapsedMs}",
                    userId, tenantId, endpoint, permission, policy.PolicyCode, policy.Id,
                    effect, resultStr, resourceContextPresent, accessVersion, elapsedMs);
            }
        }
    }

    private static LogLevel ParseLogLevel(string level, LogLevel fallback)
    {
        return level?.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" or "info" => LogLevel.Information,
            "warning" or "warn" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            _ => fallback,
        };
    }

    private static string SerializeRuleResults(List<RuleResult> ruleResults)
    {
        try
        {
            return JsonSerializer.Serialize(ruleResults.Select(r => new
            {
                field = r.Field,
                @operator = r.Operator,
                expected = r.ExpectedValue,
                actual = r.ActualValue,
                passed = r.Passed,
            }));
        }
        catch
        {
            return "[]";
        }
    }

    public static MatchedPolicy EvaluatePolicy(Policy policy, Dictionary<string, object?> attributes, int evaluationOrder)
    {
        var effectStr = policy.Effect.ToString();

        if (policy.Rules.Count == 0)
        {
            return new MatchedPolicy
            {
                PolicyCode = policy.PolicyCode,
                PolicyName = policy.Name,
                Effect = effectStr,
                Priority = policy.Priority,
                EvaluationOrder = evaluationOrder,
                Passed = true,
                Reason = "No rules defined — default match"
            };
        }

        var ruleResults = new List<RuleResult>();
        foreach (var rule in policy.Rules)
        {
            var result = EvaluateRule(rule, attributes);
            ruleResults.Add(result);
        }

        var andRules = ruleResults.Where((_, i) => policy.Rules.ElementAt(i).LogicalGroup == LogicalGroupType.And).ToList();
        var orRules = ruleResults.Where((_, i) => policy.Rules.ElementAt(i).LogicalGroup == LogicalGroupType.Or).ToList();

        bool passed;
        if (orRules.Count > 0 && andRules.Count > 0)
        {
            passed = andRules.All(r => r.Passed) && orRules.Any(r => r.Passed);
        }
        else if (orRules.Count > 0)
        {
            passed = orRules.Any(r => r.Passed);
        }
        else
        {
            passed = andRules.All(r => r.Passed);
        }

        var failedRules = ruleResults.Where(r => !r.Passed).ToList();
        var reason = passed
            ? "All rules satisfied"
            : $"Failed rules: {string.Join(", ", failedRules.Select(r => $"{r.Field} {r.Operator} {r.ExpectedValue}"))}";

        return new MatchedPolicy
        {
            PolicyCode = policy.PolicyCode,
            PolicyName = policy.Name,
            Effect = effectStr,
            Priority = policy.Priority,
            EvaluationOrder = evaluationOrder,
            Passed = passed,
            Reason = reason,
            RuleResults = ruleResults
        };
    }

    public static RuleResult EvaluateRule(PolicyRule rule, Dictionary<string, object?> attributes)
    {
        attributes.TryGetValue(rule.Field, out var rawValue);
        var actualValue = rawValue?.ToString();

        var passed = EvaluateOperator(rule.Operator, actualValue, rule.Value, rawValue);

        return new RuleResult
        {
            Field = rule.Field,
            Operator = rule.Operator.ToString(),
            ExpectedValue = rule.Value,
            ActualValue = actualValue,
            Passed = passed
        };
    }

    public static bool EvaluateOperator(RuleOperator op, string? actualValue, string expectedValue, object? rawValue)
    {
        if (actualValue == null && op != RuleOperator.NotEquals && op != RuleOperator.NotIn)
            return false;

        return op switch
        {
            RuleOperator.Equals => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            RuleOperator.NotEquals => !string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            RuleOperator.GreaterThan => TryCompareNumeric(actualValue!, expectedValue, out var cmp) && cmp > 0,
            RuleOperator.GreaterThanOrEqual => TryCompareNumeric(actualValue!, expectedValue, out var cmp2) && cmp2 >= 0,
            RuleOperator.LessThan => TryCompareNumeric(actualValue!, expectedValue, out var cmp3) && cmp3 < 0,
            RuleOperator.LessThanOrEqual => TryCompareNumeric(actualValue!, expectedValue, out var cmp4) && cmp4 <= 0,
            RuleOperator.In => EvaluateIn(actualValue!, expectedValue, rawValue),
            RuleOperator.NotIn => !EvaluateIn(actualValue ?? "", expectedValue, rawValue),
            RuleOperator.Contains => actualValue != null && actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
            RuleOperator.StartsWith => actualValue != null && actualValue.StartsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryCompareNumeric(string actual, string expected, out int result)
    {
        result = 0;
        if (!decimal.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out var a) ||
            !decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var b))
            return false;
        result = a.CompareTo(b);
        return true;
    }

    private static bool EvaluateIn(string actualValue, string expectedValue, object? rawValue)
    {
        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(expectedValue);
            if (values == null) return false;

            if (rawValue is List<string> listValue)
                return listValue.Any(v => values.Contains(v, StringComparer.OrdinalIgnoreCase));

            return values.Contains(actualValue, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            var parts = expectedValue.Split(',').Select(s => s.Trim().Trim('"', '\'')).ToList();
            return parts.Contains(actualValue, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static Dictionary<string, object?> MergeAttributes(params Dictionary<string, object?>[] sources)
    {
        var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var kvp in source)
            {
                merged.TryAdd(kvp.Key, kvp.Value);
            }
        }
        return merged;
    }

    public static string BuildCacheKey(
        string keyPrefix, string tenantId, string userId, string permission, long policyVersion,
        Dictionary<string, object?>? resourceContext)
    {
        var prefix = string.IsNullOrWhiteSpace(keyPrefix) ? "policy" : keyPrefix;
        var sb = new StringBuilder(128);
        sb.Append(prefix);
        sb.Append(':');
        sb.Append(tenantId);
        sb.Append(':');
        sb.Append(userId);
        sb.Append(':');
        sb.Append(permission);
        sb.Append(':');
        sb.Append(policyVersion);
        sb.Append(':');
        sb.Append(ComputeResourceHash(resourceContext));
        return sb.ToString();
    }

    public static string ComputeResourceHash(Dictionary<string, object?>? resourceContext)
    {
        if (resourceContext == null || resourceContext.Count == 0)
            return "empty";

        var sorted = resourceContext.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        foreach (var kvp in sorted)
        {
            sb.Append(kvp.Key.ToLowerInvariant());
            sb.Append('=');
            sb.Append(SerializeValue(kvp.Value));
            sb.Append(';');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return "v1:" + Convert.ToHexString(bytes)[..16];
    }

    private static string CanonicalizeJsonObject(JsonElement element)
    {
        var properties = element.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{p.Name.ToLowerInvariant()}:{SerializeValue(p.Value)}")
            .ToList();
        return $"{{{string.Join(",", properties)}}}";
    }

    private static string CanonicalizeJsonArray(JsonElement element)
    {
        var items = element.EnumerateArray()
            .Select(e => SerializeValue(e))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return $"[{string.Join(",", items)}]";
    }

    private static string SerializeValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => CanonicalizeJsonArray(element),
            JsonValueKind.Object => CanonicalizeJsonObject(element),
            _ => element.ToString().ToLowerInvariant(),
        };
    }

    private static string SerializeValue(object? value)
    {
        if (value == null) return "null";

        if (value is JsonElement jsonElement)
        {
            return SerializeValue(jsonElement);
        }

        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            var items = new List<string>();
            foreach (var item in enumerable)
                items.Add(item?.ToString()?.ToLowerInvariant() ?? "null");
            items.Sort(StringComparer.OrdinalIgnoreCase);
            return $"[{string.Join(",", items)}]";
        }

        return value.ToString()?.ToLowerInvariant() ?? "null";
    }
}
