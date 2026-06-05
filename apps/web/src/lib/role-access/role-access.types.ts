import type { ProductRoleValue } from '@/types';

export type LienAction =
  | 'case:create'
  | 'case:edit'
  | 'case:view'
  | 'lien:create'
  | 'lien:edit'
  | 'lien:view'
  | 'lien:offer'
  | 'lien:purchase'
  | 'offer:create'
  | 'offer:accept'
  | 'bos:view'
  | 'bos:manage'
  | 'servicing:create'
  | 'servicing:edit'
  | 'servicing:assign'
  | 'servicing:view'
  | 'contact:create'
  | 'contact:edit'
  | 'contact:view'
  | 'document:upload'
  | 'document:edit'
  | 'document:view'
  | 'user:manage'
  | 'financial:view'
  | 'workflow:view'
  | 'workflow:start'
  | 'workflow:advance'
  | 'workflow:complete';

export type LienModule =
  | 'dashboard'
  | 'cases'
  | 'liens'
  | 'servicing'
  | 'task-manager'
  | 'bill-of-sales'
  | 'contacts'
  | 'documents'
  | 'marketplace'
  | 'portfolio'
  | 'user-management'
  | 'batch-entry'
  | 'activity'
  | 'notifications';

export interface RoleAccessInfo {
  productRoles: ProductRoleValue[];
  isSeller: boolean;
  isBuyer: boolean;
  isHolder: boolean;
  isAdmin: boolean;
  isTenantAdmin: boolean;
  hasAnyLienRole: boolean;
  can: (action: LienAction) => boolean;
  canViewModule: (module: LienModule) => boolean;
}
