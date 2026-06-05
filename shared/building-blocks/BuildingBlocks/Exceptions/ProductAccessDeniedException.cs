namespace BuildingBlocks.Exceptions;

public class ProductAccessDeniedException : ForbiddenException
{
    public string ErrorCode { get; }
    public string? ProductCode { get; }
    public IReadOnlyList<string>? RequiredRoles { get; }
    public Guid? OrganizationId { get; }
    public string? DenialReason { get; }

    public ProductAccessDeniedException(
        string errorCode,
        string message,
        string? productCode = null,
        IReadOnlyList<string>? requiredRoles = null,
        Guid? organizationId = null,
        string? denialReason = null)
        : base(productCode ?? errorCode)
    {
        ErrorCode = errorCode;
        ProductCode = productCode;
        RequiredRoles = requiredRoles;
        OrganizationId = organizationId;
        DenialReason = denialReason;
    }

    public static ProductAccessDeniedException NoProductAccess(string productCode) =>
        new(
            errorCode: "PRODUCT_ACCESS_DENIED",
            message: $"Access to product '{productCode}' is denied.",
            productCode: productCode,
            denialReason: "User does not have access to this product.");

    public static ProductAccessDeniedException InsufficientProductRole(
        string productCode, IReadOnlyList<string> requiredRoles) =>
        new(
            errorCode: "PRODUCT_ROLE_REQUIRED",
            message: $"Required product role(s) for '{productCode}': {string.Join(", ", requiredRoles)}.",
            productCode: productCode,
            requiredRoles: requiredRoles,
            denialReason: "User does not have the required product role.");

    public static ProductAccessDeniedException NoOrgProductAccess(
        string productCode, Guid organizationId) =>
        new(
            errorCode: "ORG_PRODUCT_ACCESS_DENIED",
            message: $"Access to product '{productCode}' denied for organization '{organizationId}'.",
            productCode: productCode,
            organizationId: organizationId,
            denialReason: "User does not have product access for this organization.");

    public static ProductAccessDeniedException InsufficientOrgProductRole(
        string productCode, IReadOnlyList<string> requiredRoles, Guid organizationId) =>
        new(
            errorCode: "ORG_PRODUCT_ROLE_REQUIRED",
            message: $"Required product role(s) for '{productCode}' in org '{organizationId}': {string.Join(", ", requiredRoles)}.",
            productCode: productCode,
            requiredRoles: requiredRoles,
            organizationId: organizationId,
            denialReason: "User does not have the required product role for this organization.");

    public static ProductAccessDeniedException MissingPermission(string permissionCode) =>
        new(
            errorCode: "PERMISSION_DENIED",
            message: $"Required permission '{permissionCode}' is not granted.",
            denialReason: "User does not have the required permission.");
}
