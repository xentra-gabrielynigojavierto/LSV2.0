import type { ProductRoleValue } from '@/types';
import { ProductRole } from '@/types';

/**
 * Single source of truth for product metadata.
 * Used by ProductContext, TopBar, and Sidebar to drive product-aware navigation.
 */
export interface ProductDef {
  id: string;
  label: string;
  /** Remix Icon CSS class for this product */
  riIcon: string;
  routePrefix: string;
  /** Roles that grant access to this product. Empty = controlled by session flags (admin). */
  requiredRoles: ProductRoleValue[];
}

export const PRODUCT_DEFS: readonly ProductDef[] = [
  {
    id: 'careconnect',
    label: 'CareConnect',
    riIcon: 'ri-shield-cross-line',
    routePrefix: '/careconnect',
    requiredRoles: [ProductRole.CareConnectReferrer, ProductRole.CareConnectReceiver],
  },
  {
    id: 'fund',
    label: 'SynqFund',
    riIcon: 'ri-bank-line',
    routePrefix: '/fund',
    requiredRoles: [ProductRole.SynqFundReferrer, ProductRole.SynqFundFunder],
  },
  {
    id: 'lien',
    label: 'SynqLien',
    riIcon: 'ri-file-stack-line',
    routePrefix: '/lien',
    requiredRoles: [ProductRole.SynqLienSeller, ProductRole.SynqLienBuyer, ProductRole.SynqLienHolder],
  },
  {
    id: 'admin',
    label: 'Admin',
    riIcon: 'ri-settings-3-line',
    routePrefix: '/admin',
    requiredRoles: [],
  },
] as const;

/**
 * Infer the active product ID from the current pathname.
 * Returns null if the path does not map to any known product.
 */
export function inferProductIdFromPath(pathname: string): string | null {
  for (const p of PRODUCT_DEFS) {
    if (pathname === p.routePrefix || pathname.startsWith(p.routePrefix + '/')) {
      return p.id;
    }
  }
  return null;
}

export function getProductDef(id: string): ProductDef | undefined {
  return PRODUCT_DEFS.find(p => p.id === id);
}
