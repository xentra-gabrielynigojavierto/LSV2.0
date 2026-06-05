namespace Identity.Domain;

/// <summary>
/// Centralizes the mapping between OrgType code strings (used in JWT claims and
/// legacy columns) and the canonical OrganizationType catalog GUIDs (FK-based,
/// authoritative since Phase A).
///
/// Callers that receive an OrgType string and need to resolve the corresponding
/// OrganizationTypeId should use TryResolve rather than embedding the SeedIds
/// directly. This keeps the mapping in one place so that adding a new org type
/// only requires updating this file and the seeding migration.
/// </summary>
public static class OrgTypeMapper
{
    private static readonly Dictionary<string, Guid> _codeToId =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [OrgType.Internal]  = new Guid("70000000-0000-0000-0000-000000000001"),
            [OrgType.LawFirm]   = new Guid("70000000-0000-0000-0000-000000000002"),
            [OrgType.Provider]  = new Guid("70000000-0000-0000-0000-000000000003"),
            [OrgType.Funder]    = new Guid("70000000-0000-0000-0000-000000000004"),
            [OrgType.LienOwner] = new Guid("70000000-0000-0000-0000-000000000005"),
        };

    private static readonly Dictionary<Guid, string> _idToCode =
        _codeToId.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>
    /// Resolves the catalog OrganizationTypeId for the given OrgType code string.
    /// Returns null when the code is null, empty, or not recognized.
    /// </summary>
    public static Guid? TryResolve(string? orgTypeCode)
    {
        if (string.IsNullOrWhiteSpace(orgTypeCode)) return null;
        return _codeToId.TryGetValue(orgTypeCode, out var id) ? id : null;
    }

    /// <summary>
    /// Resolves the OrgType code string for the given catalog OrganizationTypeId.
    /// Returns null when the id is not recognized.
    /// </summary>
    public static string? TryResolveCode(Guid? organizationTypeId)
    {
        if (organizationTypeId is null) return null;
        return _idToCode.TryGetValue(organizationTypeId.Value, out var code) ? code : null;
    }

    /// <summary>All known OrgType codes in this catalog.</summary>
    public static IReadOnlyCollection<string> AllCodes => _codeToId.Keys.ToList().AsReadOnly();
}
