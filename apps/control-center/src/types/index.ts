// ── Platform constants ────────────────────────────────────────────────────────
// Mirror of BuildingBlocks.Authorization — keep in sync with backend
// LS-COR-AUT-006A: ProductRole values use unified PRODUCT:Role claim format.

export const SystemRole = {
  PlatformAdmin: 'PlatformAdmin',
  TenantAdmin:   'TenantAdmin',
  StandardUser:  'StandardUser',
} as const;
export type SystemRoleValue = typeof SystemRole[keyof typeof SystemRole];

export const ProductRole = {
  CareConnectReferrer:     'SYNQ_CARECONNECT:CARECONNECT_REFERRER',
  CareConnectReceiver:     'SYNQ_CARECONNECT:CARECONNECT_RECEIVER',
  SynqFundReferrer:        'SYNQ_FUND:SYNQFUND_REFERRER',
  SynqFundFunder:          'SYNQ_FUND:SYNQFUND_FUNDER',
  SynqFundApplicantPortal: 'SYNQ_FUND:SYNQFUND_APPLICANT_PORTAL',
  SynqLienSeller:          'SYNQ_LIENS:SYNQLIEN_SELLER',
  SynqLienBuyer:           'SYNQ_LIENS:SYNQLIEN_BUYER',
  SynqLienHolder:          'SYNQ_LIENS:SYNQLIEN_HOLDER',
} as const;
export type ProductRoleValue = typeof ProductRole[keyof typeof ProductRole];

export const OrgType = {
  Internal:  'INTERNAL',
  LawFirm:   'LAW_FIRM',
  Provider:  'PROVIDER',
  Funder:    'FUNDER',
  LienOwner: 'LIEN_OWNER',
} as const;
export type OrgTypeValue = typeof OrgType[keyof typeof OrgType];

// ── Session ───────────────────────────────────────────────────────────────────

/**
 * The authoritative Control Center session.
 * Populated from GET /identity/api/auth/me — never from raw JWT decode.
 */
export interface PlatformSession {
  userId:       string;
  email:        string;
  tenantId:     string;
  tenantCode:   string;
  orgId?:       string;
  orgType?:     OrgTypeValue;
  orgName?:     string;
  productRoles: ProductRoleValue[];
  systemRoles:  SystemRoleValue[];
  isPlatformAdmin:  boolean;
  isTenantAdmin:    boolean;
  hasOrg:           boolean;
  avatarDocumentId?:     string;
  phone?:                string;
  expiresAt:             Date;
  sessionTimeoutMinutes: number;
}

// ── Navigation ────────────────────────────────────────────────────────────────

export interface NavItem {
  href:   string;
  label:  string;
  icon?:  string;
  badge?: 'LIVE' | 'MOCKUP' | 'IN PROGRESS' | 'NEW';
}

export interface NavSubGroup {
  label: string;
  items: NavItem[];
}

export interface NavSection {
  heading?: string;
  items: NavItem[];
  subGroups?: NavSubGroup[];
}

/** @deprecated Use NavSection[] */
export interface NavGroup {
  id:    string;
  label: string;
  items: NavItem[];
}
