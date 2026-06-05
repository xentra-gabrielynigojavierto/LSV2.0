/**
 * admin.ts — Web app admin area types.
 *
 * UserResponse mirrors the identity service UserResponse DTO
 * returned by GET /identity/api/users.
 */

export interface UserResponse {
  id:             string;
  tenantId:       string;
  email:          string;
  firstName:      string;
  lastName:       string;
  isActive:       boolean;
  roles:          string[];
  organizationId?: string;
  orgType?:       string;
  productRoles?:  string[];
}
