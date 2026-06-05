export interface BillOfSaleResponseDto {
  id: string;
  billOfSaleNumber: string;
  externalReference?: string | null;
  status: string;
  lienId: string;
  lienOfferId: string;
  sellerOrgId: string;
  buyerOrgId: string;
  purchaseAmount: number;
  originalLienAmount: number;
  discountPercent?: number | null;
  sellerContactName?: string | null;
  buyerContactName?: string | null;
  terms?: string | null;
  notes?: string | null;
  documentId?: string | null;
  issuedAtUtc: string;
  executedAtUtc?: string | null;
  effectiveAtUtc?: string | null;
  cancelledAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PaginatedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface BillOfSaleQuery {
  search?: string;
  status?: string;
  lienId?: string;
  sellerOrgId?: string;
  buyerOrgId?: string;
  page?: number;
  pageSize?: number;
}

export interface BillOfSaleListItem {
  id: string;
  bosNumber: string;
  status: string;
  lienId: string;
  sellerOrgId: string;
  buyerOrgId: string;
  sellerContactName: string;
  buyerContactName: string;
  purchaseAmount: number;
  originalLienAmount: number;
  discountPercent: number | null;
  issuedAt: string;
  executedAt: string;
  createdAt: string;
  hasDocument: boolean;
}

export interface BillOfSaleDetail extends BillOfSaleListItem {
  lienOfferId: string;
  externalReference: string;
  terms: string;
  notes: string;
  documentId: string;
  effectiveAt: string;
  cancelledAt: string;
  updatedAt: string;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export const BOS_STATUS_LABELS: Record<string, string> = {
  Draft: 'Draft',
  Pending: 'Pending',
  Executed: 'Executed',
  Cancelled: 'Cancelled',
};

export const BOS_WORKFLOW_STEPS = ['Draft', 'Pending', 'Executed'] as const;
