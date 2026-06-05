export type { FailureCategoryKey } from '../../../../packages/notifications-utils';
export { FAILURE_CATEGORY_LABELS, formatFailureCategory } from '../../../../packages/notifications-utils';

export type ProductType = 'careconnect' | 'synqlien' | 'synqfund' | 'synqrx' | 'synqpayout';

export const PRODUCT_TYPES: ProductType[] = ['careconnect', 'synqlien', 'synqfund', 'synqrx', 'synqpayout'];

export const PRODUCT_TYPE_LABELS: Record<ProductType, string> = {
  careconnect: 'CareConnect',
  synqlien:    'SynqLien',
  synqfund:    'SynqFund',
  synqrx:      'SynqRx',
  synqpayout:  'SynqPayout',
};

export interface TenantBranding {
  id:              string;
  tenantId:        string;
  productType:     ProductType;
  brandName:       string;
  logoUrl:         string | null;
  primaryColor:    string | null;
  secondaryColor:  string | null;
  accentColor:     string | null;
  textColor:       string | null;
  backgroundColor: string | null;
  buttonRadius:    string | null;
  fontFamily:      string | null;
  emailHeaderHtml: string | null;
  emailFooterHtml: string | null;
  supportEmail:    string | null;
  supportPhone:    string | null;
  websiteUrl:      string | null;
  createdAt:       string;
  updatedAt:       string;
}

export interface BrandingListResponse {
  data: TenantBranding[];
  meta: { total: number; limit: number; offset: number };
}

export interface GlobalTemplate {
  id:              string;
  tenantId:        string | null;
  templateKey:     string;
  channel:         string;
  name:            string;
  description:     string | null;
  status:          string;
  isSystemTemplate: boolean;
  productType:     ProductType | null;
  templateScope:   string;
  editorType:      string;
  category:        string | null;
  isBrandable:     boolean;
  createdAt:       string;
  updatedAt:       string;
}

export interface GlobalTemplateVersion {
  id:                  string;
  templateId:          string;
  versionNumber:       number;
  subjectTemplate:     string | null;
  bodyTemplate:        string;
  textTemplate:        string | null;
  variablesSchemaJson: string | null;
  sampleDataJson:      string | null;
  editorJson:          string | null;
  designTokensJson:    string | null;
  layoutType:          string | null;
  status:              string;
  publishedAt:         string | null;
  createdAt:           string;
  updatedAt:           string;
}

export interface GlobalTemplateListResponse {
  data: GlobalTemplate[];
  meta: { total: number; limit: number; offset: number };
}

export interface TenantTemplate {
  id:              string;
  tenantId:        string | null;
  templateKey:     string;
  channel:         string;
  name:            string;
  description:     string | null;
  status:          string;
  isSystemTemplate: boolean;
  productType:     ProductType | null;
  templateScope:   string;
  editorType:      string;
  category:        string | null;
  isBrandable:     boolean;
  createdAt:       string;
  updatedAt:       string;
}

export interface TenantTemplateListResponse {
  data: TenantTemplate[];
  meta: { total: number; limit: number; offset: number };
}

export interface TenantTemplateVersion {
  id:                  string;
  templateId:          string;
  versionNumber:       number;
  subjectTemplate:     string | null;
  bodyTemplate:        string;
  textTemplate:        string | null;
  variablesSchemaJson: string | null;
  sampleDataJson:      string | null;
  editorJson:          string | null;
  designTokensJson:    string | null;
  layoutType:          string | null;
  status:              string;
  publishedAt:         string | null;
  createdAt:           string;
  updatedAt:           string;
}

export type OverrideStatus = 'none' | 'draft' | 'published';

export interface TemplatePreviewResult {
  templateId: string;
  versionId:  string;
  subject?:   string;
  body:       string;
  text?:      string;
}

export interface BrandedPreviewResult {
  templateId: string;
  versionId:  string;
  subject:    string;
  body:       string;
  text:       string;
  branding: {
    source:       string;
    name:         string;
    primaryColor: string;
  };
}

export interface NotifDetail {
  id:                string;
  tenantId:          string;
  channel:           string;
  status:            string;
  recipientJson:     string;
  providerUsed:      string | null;
  lastErrorMessage:  string | null;
  failureCategory:   string | null;
  blockedReason:     string | null;
  suppressionReason: string | null;
  metadataJson:      string | null;
  templateId:        string | null;
  templateKey:       string | null;
  templateName:      string | null;
  templateSource:    string | null;
  templateVersionId: string | null;
  productType:       string | null;
  subject:           string | null;
  bodyHtml:          string | null;
  bodyText:          string | null;
  createdAt:         string;
  updatedAt:         string;
}

/** Fan-out outcome stored on a Role/Org parent notification under metadataJson.fanout. */
export interface NotifFanOutSummary {
  mode?:               string | null;
  roleKey?:            string | null;
  orgId?:              string | null;
  channel:             string;
  totalResolved:       number;
  sentCount:           number;
  failedCount:         number;
  blockedCount:        number;
  skippedCount:        number;
  deliveredByChannel?: Record<string, number>;
  skippedByReason?:    Record<string, number>;
  blockedByReason?:    Record<string, number>;
  recipients?:         NotifFanOutRecipient[];
}

export interface NotifFanOutRecipient {
  userId?:         string | null;
  email?:          string | null;
  orgId?:          string | null;
  status:          string;
  reason?:         string | null;
  notificationId?: string | null;
}

export interface NotifEvent {
  id:        string;
  type:      string;
  status:    string;
  detail:    string | null;
  provider:  string | null;
  timestamp: string;
}

export interface NotifIssue {
  id:          string;
  category:    string;
  severity:    string;
  message:     string;
  detail:      string | null;
  resolvedAt:  string | null;
  createdAt:   string;
}

export interface RetryResult {
  notificationId: string;
  newNotificationId?: string;
  status:  string;
  message: string;
}

export interface ContactHealth {
  channel:      string;
  contactValue: string;
  status:       string;
  lastEvent:    string | null;
  lastEventAt:  string | null;
  bounceCount:  number;
  complaintCount: number;
  isSuppressed: boolean;
  suppressionReason: string | null;
}

export interface ContactSuppression {
  id:            string;
  channel:       string;
  contactValue:  string;
  reason:        string;
  source:        string;
  detail:        string | null;
  createdAt:     string;
}

export type ActionEligibility =
  | { eligible: true; actions: Array<'retry' | 'resend'> }
  | { eligible: false; reason: string };
