import { apiClient, ApiError } from '@/lib/api-client';
import type {
  TenantUser,
  TenantUserDetail,
  TenantGroup,
  AssignableRolesResponse,
  AccessDebugResponse,
  SimulationRequest,
  SimulationResult,
  TenantRolesListResponse,
} from '@/types/tenant';

export { ApiError };

export interface CreateUserBody {
  tenantId:  string;
  email:     string;
  password:  string;
  firstName: string;
  lastName:  string;
  roleIds?:  string[];
}

export interface InviteUserBody {
  tenantId:  string;
  email:     string;
  firstName: string;
  lastName:  string;
  roleId?:   string;
}

export interface InviteUserResponse {
  userId:       string;
  invitationId: string;
  email:        string;
  inviteToken?: string;
}

export const tenantClientApi = {
  createUser: (body: CreateUserBody) =>
    apiClient.post<TenantUser>('/identity/api/users', body),

  inviteUser: (body: InviteUserBody) =>
    apiClient.post<InviteUserResponse>('/identity/api/admin/users/invite', body),

  resendInvite: (userId: string) =>
    apiClient.post<{ invitationId: string; inviteToken?: string }>(
      `/identity/api/admin/users/${userId}/resend-invite`, {}),

  cancelInvite: (userId: string) =>
    apiClient.post<void>(`/identity/api/admin/users/${userId}/cancel-invite`, {}),

  getRoles: () =>
    apiClient.get<TenantRolesListResponse>('/identity/api/admin/roles'),

  getUserDetail: (userId: string) =>
    apiClient.get<TenantUserDetail>(`/identity/api/admin/users/${userId}`),

  activateUser: (userId: string) =>
    apiClient.post<void>(`/identity/api/admin/users/${userId}/activate`, {}),

  deactivateUser: (userId: string) =>
    apiClient.patch<void>(`/identity/api/admin/users/${userId}/deactivate`, {}),

  updatePhone: (userId: string, phone: string | null) =>
    apiClient.patch<{ phone: string | null }>(`/identity/api/admin/users/${userId}/phone`, { phone }),

  resetPassword: (userId: string) =>
    apiClient.post<{ message: string; resetToken?: string }>(`/identity/api/admin/users/${userId}/reset-password`, {}),

  assignProduct: (tenantId: string, userId: string, productCode: string) =>
    apiClient.put<void>(`/identity/api/tenants/${tenantId}/users/${userId}/products/${productCode}`, {}),

  removeProduct: (tenantId: string, userId: string, productCode: string) =>
    apiClient.delete<void>(`/identity/api/tenants/${tenantId}/users/${userId}/products/${productCode}`),

  assignRole: (userId: string, roleId: string) =>
    apiClient.post<void>(`/identity/api/admin/users/${userId}/roles`, { roleId }),

  removeRole: (userId: string, roleId: string) =>
    apiClient.delete<void>(`/identity/api/admin/users/${userId}/roles/${roleId}`),

  addToGroup: (tenantId: string, groupId: string, userId: string) =>
    apiClient.post<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/members`, { userId }),

  removeFromGroup: (tenantId: string, groupId: string, userId: string) =>
    apiClient.delete<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/members/${userId}`),

  createGroup: (tenantId: string, body: { name: string; description?: string }) =>
    apiClient.post<TenantGroup>(`/identity/api/tenants/${tenantId}/groups`, body),

  updateGroup: (tenantId: string, groupId: string, body: { name: string; description?: string }) =>
    apiClient.patch<TenantGroup>(`/identity/api/tenants/${tenantId}/groups/${groupId}`, body),

  archiveGroup: (tenantId: string, groupId: string) =>
    apiClient.delete<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}`),

  grantGroupProduct: (tenantId: string, groupId: string, productCode: string) =>
    apiClient.put<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/products/${productCode}`, {}),

  revokeGroupProduct: (tenantId: string, groupId: string, productCode: string) =>
    apiClient.delete<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/products/${productCode}`),

  assignGroupRole: (tenantId: string, groupId: string, roleCode: string, productCode?: string) =>
    apiClient.post<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/roles`, { roleCode, productCode }),

  removeGroupRole: (tenantId: string, groupId: string, assignmentId: string) =>
    apiClient.delete<void>(`/identity/api/tenants/${tenantId}/groups/${groupId}/roles/${assignmentId}`),

  getAssignableRoles: (userId: string) =>
    apiClient.get<AssignableRolesResponse>(`/identity/api/admin/users/${userId}/assignable-roles`),

  getProducts: () =>
    apiClient.get<{ id: string; name: string; code: string; description?: string; isActive: boolean }[]>('/identity/api/products'),

  getTenantProducts: (tenantId: string) =>
    apiClient.get<{ id: string; tenantId: string; productCode: string; status: string }[]>(
      `/identity/api/tenants/${tenantId}/products`),

  getUserProducts: (tenantId: string, userId: string) =>
    apiClient.get<{ id: string; tenantId: string; userId: string; productCode: string; accessStatus: string }[]>(
      `/identity/api/tenants/${tenantId}/users/${userId}/products`),

  getGroups: (tenantId: string) =>
    apiClient.get<TenantGroup[]>(`/identity/api/tenants/${tenantId}/groups`),

  getUserAccessDebug: (userId: string) =>
    apiClient.get<AccessDebugResponse>(`/identity/api/admin/users/${userId}/access-debug`),

  simulateAuthorization: (body: SimulationRequest) =>
    apiClient.post<SimulationResult>('/identity/api/admin/authorization/simulate', body),

  // ── LS-ID-TNT-013: Permission management (tenant-level) ──────────────────

  getRolePermissions: (roleId: string) =>
    apiClient.get<{ roleId: string; roleName: string; permissions: { id: string; code: string; name: string; productCode: string }[] }>(
      `/identity/api/admin/roles/${roleId}/permissions`
    ),

  assignRolePermission: (roleId: string, permissionId: string) =>
    apiClient.post<{ roleId: string; permissionId: string }>(
      `/identity/api/admin/roles/${roleId}/permissions`,
      { permissionId }
    ),

  revokeRolePermission: (roleId: string, permissionId: string) =>
    apiClient.delete<void>(
      `/identity/api/admin/roles/${roleId}/permissions/${permissionId}`
    ),
};
