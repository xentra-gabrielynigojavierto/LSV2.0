namespace Identity.Domain;

public static class ProductCodes
{
    public const string SynqFund = "SYNQ_FUND";
    public const string SynqLiens = "SYNQ_LIENS";
    public const string SynqCareConnect = "SYNQ_CARECONNECT";
    public const string SynqPay = "SYNQ_PAY";
    public const string SynqAI = "SYNQ_AI";
}

public static class ProductEligibilityConfig
{
    private static readonly Dictionary<string, HashSet<string>> _orgTypeToProducts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [OrgType.LawFirm] = [ProductCodes.SynqCareConnect, ProductCodes.SynqFund, ProductCodes.SynqLiens],
            [OrgType.Provider] = [ProductCodes.SynqCareConnect],
            [OrgType.Funder] = [ProductCodes.SynqFund],
            [OrgType.LienOwner] = [ProductCodes.SynqLiens],
            [OrgType.Internal] = [ProductCodes.SynqCareConnect, ProductCodes.SynqFund, ProductCodes.SynqLiens, ProductCodes.SynqPay, ProductCodes.SynqAI],
        };

    public static bool IsEligible(string orgType, string productCode)
    {
        if (string.IsNullOrWhiteSpace(orgType) || string.IsNullOrWhiteSpace(productCode))
            return false;

        return _orgTypeToProducts.TryGetValue(orgType, out var allowed)
               && allowed.Contains(productCode, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlySet<string> GetAllowedProducts(string orgType)
    {
        if (string.IsNullOrWhiteSpace(orgType))
            return new HashSet<string>();

        return _orgTypeToProducts.TryGetValue(orgType, out var allowed)
            ? allowed
            : new HashSet<string>();
    }

    public static IReadOnlySet<string> GetEligibleOrgTypes(string productCode)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (orgType, products) in _orgTypeToProducts)
        {
            if (products.Contains(productCode, StringComparer.OrdinalIgnoreCase))
                result.Add(orgType);
        }
        return result;
    }
}
