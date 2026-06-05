/**
 * LS-ID-TNT-015: Frontend mirror of BuildingBlocks.Authorization.PermissionCodes.
 *
 * These are the canonical permission code strings used by the backend.
 * Keep in sync with:
 *   shared/building-blocks/BuildingBlocks/Authorization/PermissionCodes.cs
 *
 * Usage:
 *   import { PermissionCodes } from '@/lib/permission-codes';
 *   const canAccept = usePermission(PermissionCodes.CC.ReferralAccept);
 *
 * Frontend checks are UX-only. Backend enforcement (LS-ID-TNT-012) is authoritative.
 */

export const PermissionCodes = {
  CC: {
    ReferralCreate:       'SYNQ_CARECONNECT.referral:create',
    ReferralReadOwn:      'SYNQ_CARECONNECT.referral:read:own',
    ReferralCancel:       'SYNQ_CARECONNECT.referral:cancel',
    ReferralReadAddressed:'SYNQ_CARECONNECT.referral:read:addressed',
    ReferralAccept:       'SYNQ_CARECONNECT.referral:accept',
    ReferralDecline:      'SYNQ_CARECONNECT.referral:decline',
    ReferralUpdateStatus: 'SYNQ_CARECONNECT.referral:update_status',
    ProviderSearch:       'SYNQ_CARECONNECT.provider:search',
    ProviderMap:          'SYNQ_CARECONNECT.provider:map',
    ProviderManage:       'SYNQ_CARECONNECT.provider:manage',
    AppointmentCreate:    'SYNQ_CARECONNECT.appointment:create',
    AppointmentUpdate:    'SYNQ_CARECONNECT.appointment:update',
    AppointmentManage:    'SYNQ_CARECONNECT.appointment:manage',
    AppointmentReadOwn:   'SYNQ_CARECONNECT.appointment:read:own',
    ScheduleManage:       'SYNQ_CARECONNECT.schedule:manage',
    DashboardRead:        'SYNQ_CARECONNECT.dashboard:read',
  },

  Lien: {
    LienCreate:   'SYNQ_LIENS.lien:create',
    LienOffer:    'SYNQ_LIENS.lien:offer',
    LienReadOwn:  'SYNQ_LIENS.lien:read:own',
    LienBrowse:   'SYNQ_LIENS.lien:browse',
    LienPurchase: 'SYNQ_LIENS.lien:purchase',
    LienReadHeld: 'SYNQ_LIENS.lien:read:held',
    LienService:  'SYNQ_LIENS.lien:service',
    LienSettle:   'SYNQ_LIENS.lien:settle',
    LienSell:     'SYNQ_LIENS.lien:sell',
  },

  Fund: {
    ApplicationCreate:        'SYNQ_FUND.application:create',
    ApplicationRefer:         'SYNQ_FUND.application:refer',
    ApplicationReadOwn:       'SYNQ_FUND.application:read:own',
    ApplicationCancel:        'SYNQ_FUND.application:cancel',
    ApplicationReadAddressed: 'SYNQ_FUND.application:read:addressed',
    ApplicationEvaluate:      'SYNQ_FUND.application:evaluate',
    ApplicationApprove:       'SYNQ_FUND.application:approve',
    ApplicationDecline:       'SYNQ_FUND.application:decline',
    ApplicationStatusView:    'SYNQ_FUND.application:status:view',
    PartyCreate:              'SYNQ_FUND.party:create',
    PartyReadOwn:             'SYNQ_FUND.party:read:own',
  },

  Tenant: {
    UsersView:         'TENANT.users:view',
    UsersManage:       'TENANT.users:manage',
    GroupsManage:      'TENANT.groups:manage',
    RolesAssign:       'TENANT.roles:assign',
    ProductsAssign:    'TENANT.products:assign',
    SettingsManage:    'TENANT.settings:manage',
    AuditView:         'TENANT.audit:view',
    InvitationsManage: 'TENANT.invitations:manage',
  },

  Insights: {
    DashboardView:   'SYNQ_INSIGHTS.dashboard:view',
    ReportsView:     'SYNQ_INSIGHTS.reports:view',
    ReportsRun:      'SYNQ_INSIGHTS.reports:run',
    ReportsExport:   'SYNQ_INSIGHTS.reports:export',
    ReportsBuild:    'SYNQ_INSIGHTS.reports:build',
    SchedulesManage: 'SYNQ_INSIGHTS.schedules:manage',
    SchedulesRun:    'SYNQ_INSIGHTS.schedules:run',
  },
} as const;

export type PermissionCode =
  | typeof PermissionCodes.CC[keyof typeof PermissionCodes.CC]
  | typeof PermissionCodes.Lien[keyof typeof PermissionCodes.Lien]
  | typeof PermissionCodes.Fund[keyof typeof PermissionCodes.Fund]
  | typeof PermissionCodes.Tenant[keyof typeof PermissionCodes.Tenant]
  | typeof PermissionCodes.Insights[keyof typeof PermissionCodes.Insights];
