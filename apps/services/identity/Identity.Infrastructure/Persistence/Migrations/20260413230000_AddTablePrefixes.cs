using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

  #nullable disable

  namespace Identity.Infrastructure.Persistence.Migrations;

  [DbContext(typeof(IdentityDbContext))]
  [Migration("20260413230000_AddTablePrefixes")]
  public partial class AddTablePrefixes : Migration
  {
      protected override void Up(MigrationBuilder migrationBuilder)
      {
          var renames = new[]
          {
              ("AccessGroups", "idt_AccessGroups"),
              ("AccessGroupMemberships", "idt_AccessGroupMemberships"),
              ("AuditLogs", "idt_AuditLogs"),
              ("Capabilities", "idt_Capabilities"),
              ("GroupProductAccess", "idt_GroupProductAccess"),
              ("GroupRoleAssignments", "idt_GroupRoleAssignments"),
              ("Organizations", "idt_Organizations"),
              ("OrganizationDomains", "idt_OrganizationDomains"),
              ("OrganizationProducts", "idt_OrganizationProducts"),
              ("OrganizationRelationships", "idt_OrganizationRelationships"),
              ("OrganizationTypes", "idt_OrganizationTypes"),
              ("PasswordResetTokens", "idt_PasswordResetTokens"),
              ("PermissionPolicies", "idt_PermissionPolicies"),
              ("Policies", "idt_Policies"),
              ("PolicyRules", "idt_PolicyRules"),
              ("Products", "idt_Products"),
              ("ProductOrganizationTypeRules", "idt_ProductOrganizationTypeRules"),
              ("ProductRelationshipTypeRules", "idt_ProductRelationshipTypeRules"),
              ("ProductRoles", "idt_ProductRoles"),
              ("RelationshipTypes", "idt_RelationshipTypes"),
              ("Roles", "idt_Roles"),
              ("RoleCapabilities", "idt_RoleCapabilities"),
              ("RoleCapabilityAssignments", "idt_RoleCapabilityAssignments"),
              ("ScopedRoleAssignments", "idt_ScopedRoleAssignments"),
              ("Tenants", "idt_Tenants"),
              ("TenantDomains", "idt_TenantDomains"),
              ("TenantProducts", "idt_TenantProducts"),
              ("TenantProductEntitlements", "idt_TenantProductEntitlements"),
              ("Users", "idt_Users"),
              ("UserInvitations", "idt_UserInvitations"),
              ("UserOrganizationMemberships", "idt_UserOrganizationMemberships"),
              ("UserProductAccess", "idt_UserProductAccess"),
              ("UserRoleAssignments", "idt_UserRoleAssignments"),
          };

          foreach (var (oldName, newName) in renames)
          {
              migrationBuilder.Sql($@"
                  SET @tbl_exists = (SELECT COUNT(*) FROM information_schema.tables
                      WHERE table_schema = DATABASE() AND table_name = '{oldName}');
                  SET @new_exists = (SELECT COUNT(*) FROM information_schema.tables
                      WHERE table_schema = DATABASE() AND table_name = '{newName}');
                  SET @sql = IF(@tbl_exists > 0 AND @new_exists = 0,
                      'RENAME TABLE `{oldName}` TO `{newName}`', 'SELECT 1');
                  PREPARE stmt FROM @sql;
                  EXECUTE stmt;
                  DEALLOCATE PREPARE stmt;");
          }
      }

      protected override void Down(MigrationBuilder migrationBuilder)
      {
          var renames = new[]
          {
              ("idt_AccessGroups", "AccessGroups"),
              ("idt_AccessGroupMemberships", "AccessGroupMemberships"),
              ("idt_AuditLogs", "AuditLogs"),
              ("idt_Capabilities", "Capabilities"),
              ("idt_GroupProductAccess", "GroupProductAccess"),
              ("idt_GroupRoleAssignments", "GroupRoleAssignments"),
              ("idt_Organizations", "Organizations"),
              ("idt_OrganizationDomains", "OrganizationDomains"),
              ("idt_OrganizationProducts", "OrganizationProducts"),
              ("idt_OrganizationRelationships", "OrganizationRelationships"),
              ("idt_OrganizationTypes", "OrganizationTypes"),
              ("idt_PasswordResetTokens", "PasswordResetTokens"),
              ("idt_PermissionPolicies", "PermissionPolicies"),
              ("idt_Policies", "Policies"),
              ("idt_PolicyRules", "PolicyRules"),
              ("idt_Products", "Products"),
              ("idt_ProductOrganizationTypeRules", "ProductOrganizationTypeRules"),
              ("idt_ProductRelationshipTypeRules", "ProductRelationshipTypeRules"),
              ("idt_ProductRoles", "ProductRoles"),
              ("idt_RelationshipTypes", "RelationshipTypes"),
              ("idt_Roles", "Roles"),
              ("idt_RoleCapabilities", "RoleCapabilities"),
              ("idt_RoleCapabilityAssignments", "RoleCapabilityAssignments"),
              ("idt_ScopedRoleAssignments", "ScopedRoleAssignments"),
              ("idt_Tenants", "Tenants"),
              ("idt_TenantDomains", "TenantDomains"),
              ("idt_TenantProducts", "TenantProducts"),
              ("idt_TenantProductEntitlements", "TenantProductEntitlements"),
              ("idt_Users", "Users"),
              ("idt_UserInvitations", "UserInvitations"),
              ("idt_UserOrganizationMemberships", "UserOrganizationMemberships"),
              ("idt_UserProductAccess", "UserProductAccess"),
              ("idt_UserRoleAssignments", "UserRoleAssignments"),
          };

          foreach (var (oldName, newName) in renames)
          {
              migrationBuilder.Sql($@"
                  SET @tbl_exists = (SELECT COUNT(*) FROM information_schema.tables
                      WHERE table_schema = DATABASE() AND table_name = '{oldName}');
                  SET @new_exists = (SELECT COUNT(*) FROM information_schema.tables
                      WHERE table_schema = DATABASE() AND table_name = '{newName}');
                  SET @sql = IF(@tbl_exists > 0 AND @new_exists = 0,
                      'RENAME TABLE `{oldName}` TO `{newName}`', 'SELECT 1');
                  PREPARE stmt FROM @sql;
                  EXECUTE stmt;
                  DEALLOCATE PREPARE stmt;");
          }
      }
  }
