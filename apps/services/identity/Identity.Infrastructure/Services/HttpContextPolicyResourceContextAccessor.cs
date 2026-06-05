using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Http;

namespace Identity.Infrastructure.Services;

public class HttpContextPolicyResourceContextAccessor : IPolicyResourceContextAccessor
{
    private const string ItemKey = "PolicyResourceContext";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextPolicyResourceContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Dictionary<string, object?> GetResourceContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (httpContext.Items.TryGetValue(ItemKey, out var ctx) && ctx is Dictionary<string, object?> dict)
            return dict;

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public void SetResourceContext(Dictionary<string, object?> context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
            httpContext.Items[ItemKey] = context;
    }

    public void MergeResourceContext(string key, object? value)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        if (httpContext.Items.TryGetValue(ItemKey, out var ctx) && ctx is Dictionary<string, object?> dict)
        {
            dict[key] = value;
        }
        else
        {
            var newDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [key] = value
            };
            httpContext.Items[ItemKey] = newDict;
        }
    }
}
