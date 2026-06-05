import { ProductRole } from '@/types';
import type { ProductRoleValue } from '@/types';
import type { LienAction, LienModule, RoleAccessInfo } from './role-access.types';

export function buildRoleAccess(
  productRoles: ProductRoleValue[],
  isPlatformAdmin: boolean,
  isTenantAdmin: boolean,
  isSellMode: boolean,
): RoleAccessInfo {
  const isSeller = productRoles.includes(ProductRole.SynqLienSeller);
  const isBuyer  = productRoles.includes(ProductRole.SynqLienBuyer);
  const isHolder = productRoles.includes(ProductRole.SynqLienHolder);
  const isAdmin  = isPlatformAdmin;
  const hasAnyLienRole = isSeller || isBuyer || isHolder || isAdmin || isTenantAdmin;

  function can(action: LienAction): boolean {
    if (isAdmin || isTenantAdmin) return isSellMode || !isSellOnlyAction(action);

    switch (action) {
      case 'case:create':
      case 'case:edit':
        return isSeller || isHolder;
      case 'case:view':
        return hasAnyLienRole;

      case 'lien:create':
      case 'lien:edit':
        return isSeller;
      case 'lien:view':
        return hasAnyLienRole;

      case 'lien:offer':
      case 'offer:create':
        return isSellMode && isSeller;
      case 'lien:purchase':
      case 'offer:accept':
        return isSellMode && isBuyer;

      case 'bos:view':
        return isSellMode && (isSeller || isBuyer || isHolder);
      case 'bos:manage':
        return isSellMode && (isSeller || isBuyer);

      case 'servicing:view':
        return hasAnyLienRole;
      case 'servicing:create':
      case 'servicing:edit':
      case 'servicing:assign':
        return isSeller || isHolder;

      case 'contact:view':
        return hasAnyLienRole;
      case 'contact:create':
      case 'contact:edit':
        return isSeller || isHolder;

      case 'document:view':
        return hasAnyLienRole;
      case 'document:upload':
      case 'document:edit':
        return isSeller || isHolder;

      case 'user:manage':
        return false;

      case 'financial:view':
        return isSeller || isBuyer || isHolder;

      // E8.1 — workflow surface. View mirrors case:view; start mirrors
      // case:edit so only Sellers/Holders can drive new workflow instances.
      case 'workflow:view':
        return hasAnyLienRole;
      case 'workflow:start':
      case 'workflow:advance':
      case 'workflow:complete':
        // E8.4 — progression is allowed for the same SynqLien roles that
        // can drive workflow creation. Backend authorisation (Liens.Api +
        // hardened Flow ownership endpoints) remains authoritative.
        return isSeller || isHolder;

      default:
        return false;
    }
  }

  function canViewModule(module: LienModule): boolean {
    if (isAdmin || isTenantAdmin) return isSellMode || !isSellOnlyModule(module);

    switch (module) {
      case 'dashboard':
      case 'activity':
      case 'notifications':
        return hasAnyLienRole;
      case 'cases':
        return hasAnyLienRole;
      case 'liens':
        return hasAnyLienRole;
      case 'servicing':
      case 'task-manager':
        return hasAnyLienRole;
      case 'bill-of-sales':
        return isSellMode && (isSeller || isBuyer || isHolder);
      case 'contacts':
        return hasAnyLienRole;
      case 'documents':
        return hasAnyLienRole;
      case 'marketplace':
        return isSellMode && isBuyer;
      case 'portfolio':
        return isSellMode && (isBuyer || isHolder);
      case 'user-management':
        return false;
      case 'batch-entry':
        return isSeller;
      default:
        return false;
    }
  }

  return {
    productRoles,
    isSeller,
    isBuyer,
    isHolder,
    isAdmin,
    isTenantAdmin,
    hasAnyLienRole,
    can,
    canViewModule,
  };
}

function isSellOnlyAction(action: LienAction): boolean {
  return [
    'lien:offer', 'offer:create', 'offer:accept',
    'lien:purchase', 'bos:view', 'bos:manage',
  ].includes(action);
}

function isSellOnlyModule(module: LienModule): boolean {
  return ['bill-of-sales', 'marketplace', 'portfolio'].includes(module);
}
