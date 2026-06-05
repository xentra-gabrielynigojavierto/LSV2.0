using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BuildingBlocks.Authorization.Filters;

public static class ProductAuthorizationExtensions
{
    public static RouteHandlerBuilder RequireProductAccess(
        this RouteHandlerBuilder builder, string productCode) =>
        builder.AddEndpointFilter(new RequireProductAccessFilter(productCode));

    public static RouteGroupBuilder RequireProductAccess(
        this RouteGroupBuilder builder, string productCode) =>
        builder.AddEndpointFilter(new RequireProductAccessFilter(productCode));

    public static RouteHandlerBuilder RequireProductRole(
        this RouteHandlerBuilder builder, string productCode, params string[] requiredRoles) =>
        builder.AddEndpointFilter(new RequireProductRoleFilter(productCode, requiredRoles));

    public static RouteHandlerBuilder RequireOrgProductAccess(
        this RouteHandlerBuilder builder, string productCode) =>
        builder.AddEndpointFilter(new RequireOrgProductAccessFilter(productCode));

    public static RouteHandlerBuilder RequirePermission(
        this RouteHandlerBuilder builder, string permissionCode, params string[] fallbackRoles) =>
        builder.AddEndpointFilter(new RequirePermissionFilter(permissionCode, fallbackRoles.Length > 0 ? fallbackRoles : null));

    public static RouteGroupBuilder RequirePermission(
        this RouteGroupBuilder builder, string permissionCode, params string[] fallbackRoles) =>
        builder.AddEndpointFilter(new RequirePermissionFilter(permissionCode, fallbackRoles.Length > 0 ? fallbackRoles : null));

    public static RouteHandlerBuilder RequireSellMode(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(new RequireSellModeFilter());

    public static RouteGroupBuilder RequireSellMode(this RouteGroupBuilder builder) =>
        builder.AddEndpointFilter(new RequireSellModeFilter());
}
