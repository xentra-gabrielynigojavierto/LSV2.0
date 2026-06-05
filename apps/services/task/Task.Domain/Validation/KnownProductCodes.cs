using BuildingBlocks.Authorization;

namespace Task.Domain.Validation;

/// <summary>
/// TASK-B05 (TASK-014) — canonical product-code validator.
/// Validates <c>sourceProductCode</c> values against the platform-wide
/// <see cref="ProductCodes"/> registry so invalid codes are rejected
/// deterministically at the service layer rather than silently accepted.
/// </summary>
public static class KnownProductCodes
{
    private static readonly HashSet<string> _valid = new(StringComparer.OrdinalIgnoreCase)
    {
        ProductCodes.SynqCareConnect,
        ProductCodes.SynqFund,
        ProductCodes.SynqLiens,
        ProductCodes.SynqPay,
        ProductCodes.SynqInsights,
        ProductCodes.SynqComms,
        // SynqPlatform is intentionally excluded — it is a virtual pseudo-product
        // used only for permission catalog anchoring, never for task data.
    };

    /// <summary>
    /// Returns the canonical (upper-invariant, trimmed) form of <paramref name="productCode"/>.
    /// </summary>
    /// <param name="productCode">The raw product code supplied by the caller.</param>
    /// <returns>Trimmed, upper-invariant value.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="productCode"/> is not in the canonical product registry.
    /// </exception>
    public static string Validate(string productCode)
    {
        var normalized = productCode.Trim().ToUpperInvariant();
        if (!_valid.Contains(normalized))
            throw new ArgumentException(
                $"'{productCode}' is not a recognized product code. " +
                $"Valid values: {string.Join(", ", _valid.OrderBy(x => x))}",
                nameof(productCode));
        return normalized;
    }

    /// <summary>
    /// Validates <paramref name="productCode"/> only when it is non-null/non-empty.
    /// Returns the normalized form, or null if the input was null/empty.
    /// </summary>
    public static string? ValidateOptional(string? productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode)) return null;
        return Validate(productCode);
    }
}
