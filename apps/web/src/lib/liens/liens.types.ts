export interface LienResponseDto {
  id: string;
  lienNumber: string;
  externalReference?: string | null;
  lienType: string;
  status: string;
  caseId?: string | null;
  facilityId?: string | null;
  originalAmount: number;
  currentBalance?: number | null;
  offerPrice?: number | null;
  purchasePrice?: number | null;
  payoffAmount?: number | null;
  jurisdiction?: string | null;
  isConfidential: boolean;
  subjectFirstName?: string | null;
  subjectLastName?: string | null;
  subjectDisplayName?: string | null;
  orgId: string;
  sellingOrgId?: string | null;
  buyingOrgId?: string | null;
  holdingOrgId?: string | null;
  incidentDate?: string | null;
  description?: string | null;
  openedAtUtc?: string | null;
  closedAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface LienOfferResponseDto {
  id: string;
  lienId: string;
  offerAmount: number;
  status: string;
  buyerOrgId: string;
  sellerOrgId: string;
  notes?: string | null;
  responseNotes?: string | null;
  externalReference?: string | null;
  offeredAtUtc: string;
  expiresAtUtc?: string | null;
  respondedAtUtc?: string | null;
  withdrawnAtUtc?: string | null;
  isExpired: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SaleFinalizationResultDto {
  acceptedOfferId: string;
  acceptedOfferStatus: string;
  lienId: string;
  finalLienStatus: string;
  billOfSaleId: string;
  billOfSaleNumber: string;
  billOfSaleStatus: string;
  purchaseAmount: number;
  originalLienAmount: number;
  discountPercent?: number | null;
  documentId?: string | null;
  competingOffersRejected: number;
  finalizedAtUtc: string;
}

export interface PaginatedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateLienRequestDto {
  lienNumber: string;
  externalReference?: string;
  lienType: string;
  caseId?: string;
  facilityId?: string;
  originalAmount: number;
  jurisdiction?: string;
  isConfidential: boolean;
  subjectFirstName?: string;
  subjectLastName?: string;
  incidentDate?: string;
  description?: string;
}

export interface UpdateLienRequestDto {
  externalReference?: string;
  lienType: string;
  caseId?: string;
  facilityId?: string;
  originalAmount: number;
  jurisdiction?: string;
  isConfidential?: boolean;
  subjectFirstName?: string;
  subjectLastName?: string;
  incidentDate?: string;
  description?: string;
}

export interface CreateLienOfferRequestDto {
  lienId: string;
  offerAmount: number;
  notes?: string;
  expiresAtUtc?: string;
}

export interface LiensQuery {
  search?: string;
  status?: string;
  lienType?: string;
  caseId?: string;
  page?: number;
  pageSize?: number;
}

export interface LienListItem {
  id: string;
  lienNumber: string;
  lienType: string;
  lienTypeLabel: string;
  status: string;
  caseId: string;
  originalAmount: number;
  currentBalance: number | null;
  offerPrice: number | null;
  purchasePrice: number | null;
  jurisdiction: string;
  isConfidential: boolean;
  subjectName: string;
  createdAt: string;
  updatedAt: string;
}

export interface LienDetail {
  id: string;
  lienNumber: string;
  externalReference: string;
  lienType: string;
  lienTypeLabel: string;
  status: string;
  caseId: string;
  originalAmount: number;
  currentBalance: number | null;
  offerPrice: number | null;
  purchasePrice: number | null;
  payoffAmount: number | null;
  jurisdiction: string;
  isConfidential: boolean;
  subjectName: string;
  subjectFirstName: string;
  subjectLastName: string;
  orgId: string;
  sellingOrgId: string;
  buyingOrgId: string;
  holdingOrgId: string;
  incidentDate: string;
  description: string;
  openedAt: string;
  closedAt: string;
  createdAt: string;
  updatedAt: string;
}

export interface LienOfferItem {
  id: string;
  lienId: string;
  offerAmount: number;
  status: string;
  buyerOrgId: string;
  sellerOrgId: string;
  notes: string;
  responseNotes: string;
  offeredAt: string;
  expiresAt: string;
  respondedAt: string;
  isExpired: boolean;
  createdAt: string;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
