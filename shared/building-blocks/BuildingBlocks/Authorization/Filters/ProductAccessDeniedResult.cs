using BuildingBlocks.Exceptions;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Authorization.Filters;

public static class ProductAccessDeniedResult
{
    public static IResult Create(ProductAccessDeniedException ex) =>
        Results.Json(new
        {
            error = new
            {
                code = ex.ErrorCode,
                message = ex.DenialReason ?? ex.Message,
                productCode = ex.ProductCode,
                requiredRoles = ex.RequiredRoles,
                organizationId = ex.OrganizationId
            }
        }, statusCode: StatusCodes.Status403Forbidden);

    public static IResult Create(
        string errorCode,
        string message,
        string? productCode = null,
        IReadOnlyList<string>? requiredRoles = null,
        Guid? organizationId = null) =>
        Results.Json(new
        {
            error = new
            {
                code = errorCode,
                message,
                productCode,
                requiredRoles,
                organizationId
            }
        }, statusCode: StatusCodes.Status403Forbidden);
}
