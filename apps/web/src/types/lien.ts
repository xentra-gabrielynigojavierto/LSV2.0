// ── Lien status ───────────────────────────────────────────────────────────────

export const LienStatus = {
  Draft:     'Draft',
  Offered:   'Offered',
  Sold:      'Sold',
  Withdrawn: 'Withdrawn',
} as const;
export type LienStatusValue = typeof LienStatus[keyof typeof LienStatus];

// ── Lien type codes ───────────────────────────────────────────────────────────

export const LienType = {
  MedicalLien:           'MedicalLien',
  AttorneyLien:          'AttorneyLien',
  SettlementAdvance:     'SettlementAdvance',
  WorkersCompLien:       'WorkersCompLien',
  PropertyLien:          'PropertyLien',
  Other:                 'Other',
} as const;
export type LienTypeValue = typeof LienType[keyof typeof LienType];

export const LIEN_TYPE_LABELS: Record<string, string> = {
  MedicalLien:       'Medical Lien',
  AttorneyLien:      'Attorney Lien',
  SettlementAdvance: 'Settlement Advance',
  WorkersCompLien:   "Workers' Comp Lien",
  PropertyLien:      'Property Lien',
  Other:             'Other',
};

// ── Subject party snapshot ────────────────────────────────────────────────────

export interface PartySnapshot {
  firstName?: string;
  lastName?:  string;
  caseRef?:   string;
}

// ── Org snapshot ──────────────────────────────────────────────────────────────

export interface OrgSnapshot {
  orgId:   string;
  orgName: string;
}

// ── Lien offer (buyer → seller negotiation) ───────────────────────────────────

export interface LienOfferSummary {
  id:            string;
  lienId:        string;
  buyerOrgId:    string;
  buyerOrgName?: string;
  offerAmount:   number;
  notes?:        string;
  status:        'Pending' | 'Accepted' | 'Rejected' | 'Withdrawn';
  createdAtUtc:  string;
  updatedAtUtc:  string;
}

// ── Status history ────────────────────────────────────────────────────────────

export interface LienStatusHistoryItem {
  status:        string;
  occurredAtUtc: string;
  label:         string;
  actorOrgName?: string;
}

// ── Lien summary (list row) ───────────────────────────────────────────────────

export interface LienSummary {
  id:                   string;
  tenantId:             string;
  lienNumber:           string;
  lienType:             string;
  status:               string;
  originalAmount:       number;
  offerPrice?:          number;
  purchasePrice?:       number;
  jurisdiction?:        string;
  caseRef?:             string;
  isConfidential:       boolean;
  subjectParty?:        PartySnapshot;
  sellingOrg?:          OrgSnapshot;
  buyingOrg?:           OrgSnapshot;
  holdingOrg?:          OrgSnapshot;
  createdAtUtc:         string;
  updatedAtUtc:         string;
}

// ── Lien detail (full record) ─────────────────────────────────────────────────

export interface LienDetail extends LienSummary {
  incidentDate?:       string;      // yyyy-MM-dd
  description?:        string;
  offerExpiresAtUtc?:  string;
  offerNotes?:         string;
  sellingOrgId?:       string;
  buyingOrgId?:        string;
  holdingOrgId?:       string;
  subjectPartyId?:     string;
  offers?:             LienOfferSummary[];
  createdByUserId?:    string;
  updatedByUserId?:    string;
}

// ── Case status ──────────────────────────────────────────────────────────────

export const CaseStatus = {
  PreDemand:     'PreDemand',
  DemandSent:    'DemandSent',
  InNegotiation: 'InNegotiation',
  CaseSettled:   'CaseSettled',
  Closed:        'Closed',
} as const;
export type CaseStatusValue = typeof CaseStatus[keyof typeof CaseStatus];

export const CASE_STATUS_LABELS: Record<string, string> = {
  PreDemand:     'Pre-Demand',
  DemandSent:    'Demand Sent',
  InNegotiation: 'In Negotiation',
  CaseSettled:   'Case Settled',
  Closed:        'Closed',
};

// ── Case summary ─────────────────────────────────────────────────────────────

export interface CaseSummary {
  id:               string;
  caseNumber:       string;
  status:           string;
  clientName:       string;
  lawFirm:          string;
  medicalFacility:  string;
  dateOfIncident:   string;
  totalLienAmount:  number;
  lienCount:        number;
  assignedTo:       string;
  createdAtUtc:     string;
  updatedAtUtc:     string;
}

export interface CaseDetail extends CaseSummary {
  description?:     string;
  clientDob?:       string;
  clientPhone?:     string;
  clientEmail?:     string;
  clientAddress?:   string;
  insuranceCarrier?: string;
  policyNumber?:    string;
  claimNumber?:     string;
  demandAmount?:    number;
  settlementAmount?: number;
  notes?:           string;
}

// ── Bill of Sale ─────────────────────────────────────────────────────────────

export const BillOfSaleStatus = {
  Draft:     'Draft',
  Pending:   'Pending',
  Executed:  'Executed',
  Cancelled: 'Cancelled',
} as const;

export interface BillOfSaleSummary {
  id:               string;
  bosNumber:        string;
  status:           string;
  lienId:           string;
  lienNumber:       string;
  caseNumber?:      string;
  sellerOrg:        string;
  buyerOrg:         string;
  saleAmount:       number;
  executionDate?:   string;
  createdAtUtc:     string;
}

export interface BillOfSaleDetail extends BillOfSaleSummary {
  originalLienAmount: number;
  discountPercent:    number;
  sellerContact?:     string;
  buyerContact?:      string;
  terms?:             string;
  notes?:             string;
}

// ── Servicing ────────────────────────────────────────────────────────────────

export const ServicingStatus = {
  Pending:    'Pending',
  InProgress: 'InProgress',
  Completed:  'Completed',
  Escalated:  'Escalated',
  OnHold:     'OnHold',
} as const;

export const ServicingPriority = {
  Low:    'Low',
  Normal: 'Normal',
  High:   'High',
  Urgent: 'Urgent',
} as const;

export interface ServicingItem {
  id:             string;
  taskNumber:     string;
  taskType:       string;
  status:         string;
  priority:       string;
  caseNumber?:    string;
  lienNumber?:    string;
  assignedTo:     string;
  description:    string;
  dueDate:        string;
  createdAtUtc:   string;
  updatedAtUtc:   string;
}

export interface ServicingDetail extends ServicingItem {
  notes?:         string;
  resolution?:    string;
  linkedCaseId?:  string;
  linkedLienId?:  string;
  linkedContactId?: string;
  history:        { action: string; timestamp: string; actor: string; note?: string }[];
}

// ── Contact ──────────────────────────────────────────────────────────────────

export const ContactType = {
  LawFirm:      'LawFirm',
  Provider:     'Provider',
  LienHolder:   'LienHolder',
  CaseManager:  'CaseManager',
  InternalUser: 'InternalUser',
} as const;

export const CONTACT_TYPE_LABELS: Record<string, string> = {
  LawFirm:      'Law Firm',
  Provider:     'Provider',
  LienHolder:   'Lien Holder',
  CaseManager:  'Case Manager',
  InternalUser: 'Internal User',
};

export interface ContactSummary {
  id:             string;
  contactType:    string;
  name:           string;
  organization:   string;
  email:          string;
  phone:          string;
  city:           string;
  state:          string;
  activeCases:    number;
  createdAtUtc:   string;
}

export interface ContactDetail extends ContactSummary {
  title?:         string;
  address?:       string;
  zipCode?:       string;
  notes?:         string;
  website?:       string;
  fax?:           string;
}

// ── Document ─────────────────────────────────────────────────────────────────

export const DocumentStatus = {
  Pending:    'Pending',
  Processing: 'Processing',
  Completed:  'Completed',
  Failed:     'Failed',
  Archived:   'Archived',
} as const;

export const DocumentCategory = {
  MedicalRecord: 'MedicalRecord',
  LegalFiling:   'LegalFiling',
  Financial:     'Financial',
  Correspondence: 'Correspondence',
  Contract:      'Contract',
  Other:         'Other',
} as const;

export const DOCUMENT_CATEGORY_LABELS: Record<string, string> = {
  MedicalRecord:  'Medical Record',
  LegalFiling:    'Legal Filing',
  Financial:      'Financial',
  Correspondence: 'Correspondence',
  Contract:       'Contract',
  Other:          'Other',
};

export interface DocumentSummary {
  id:             string;
  documentNumber: string;
  fileName:       string;
  category:       string;
  status:         string;
  linkedEntity:   string;
  linkedEntityId: string;
  uploadedBy:     string;
  fileSize:       string;
  createdAtUtc:   string;
}

export interface DocumentDetail extends DocumentSummary {
  description?:   string;
  mimeType:       string;
  version:        number;
  tags:           string[];
  processingNotes?: string;
}

// ── User Management ──────────────────────────────────────────────────────────

export const UserStatus = {
  Active:   'Active',
  Inactive: 'Inactive',
  Invited:  'Invited',
  Locked:   'Locked',
} as const;

export interface LienUser {
  id:             string;
  name:           string;
  email:          string;
  role:           string;
  status:         string;
  department:     string;
  lastLoginAtUtc?: string;
  createdAtUtc:   string;
}

export interface LienUserDetail extends LienUser {
  phone?:         string;
  title?:         string;
  permissions:    string[];
  activityLog:    { action: string; timestamp: string }[];
}

// ── Requests ──────────────────────────────────────────────────────────────────

export interface CreateLienRequest {
  lienType:              string;
  originalAmount:        number;
  jurisdiction?:         string;
  caseRef?:              string;
  incidentDate?:         string;    // yyyy-MM-dd
  description?:          string;
  isConfidential:        boolean;
  // Subject party inline snapshot
  subjectFirstName?:     string;
  subjectLastName?:      string;
}

export interface OfferLienRequest {
  offerPrice:     number;
  offerNotes?:    string;
  expiresAtUtc?:  string;  // ISO 8601
}

export interface SubmitLienOfferRequest {
  offerAmount: number;
  notes?:      string;
}

export interface PurchaseLienRequest {
  purchaseAmount: number;
  notes?:         string;
}

// ── Search params ─────────────────────────────────────────────────────────────

export interface LienSearchParams {
  status?:     string;
  lienType?:   string;
  jurisdiction?: string;
  minAmount?:  number;
  maxAmount?:  number;
  page?:       number;
  pageSize?:   number;
}
