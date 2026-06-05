import type {
  BillOfSaleResponseDto,
  PaginatedResultDto,
  BillOfSaleListItem,
  BillOfSaleDetail,
  PaginationMeta,
} from './billofsale.types';

function safeString(val: string | null | undefined): string {
  return val ?? '';
}

function formatDateField(val: string | null | undefined): string {
  if (!val) return '';
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return val;
  }
}

export function mapBosToListItem(dto: BillOfSaleResponseDto): BillOfSaleListItem {
  return {
    id: dto.id,
    bosNumber: dto.billOfSaleNumber,
    status: dto.status,
    lienId: dto.lienId,
    sellerOrgId: dto.sellerOrgId,
    buyerOrgId: dto.buyerOrgId,
    sellerContactName: safeString(dto.sellerContactName),
    buyerContactName: safeString(dto.buyerContactName),
    purchaseAmount: dto.purchaseAmount,
    originalLienAmount: dto.originalLienAmount,
    discountPercent: dto.discountPercent ?? null,
    issuedAt: formatDateField(dto.issuedAtUtc),
    executedAt: formatDateField(dto.executedAtUtc),
    createdAt: formatDateField(dto.createdAtUtc),
    hasDocument: !!dto.documentId,
  };
}

export function mapBosToDetail(dto: BillOfSaleResponseDto): BillOfSaleDetail {
  return {
    ...mapBosToListItem(dto),
    lienOfferId: dto.lienOfferId,
    externalReference: safeString(dto.externalReference),
    terms: safeString(dto.terms),
    notes: safeString(dto.notes),
    documentId: safeString(dto.documentId),
    effectiveAt: formatDateField(dto.effectiveAtUtc),
    cancelledAt: formatDateField(dto.cancelledAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
  };
}

export function mapBosPagination<T>(result: PaginatedResultDto<T>): PaginationMeta {
  return {
    page: result.page,
    pageSize: result.pageSize,
    totalCount: result.totalCount,
    totalPages: Math.ceil(result.totalCount / Math.max(result.pageSize, 1)),
  };
}

export function formatCurrency(amount?: number): string {
  if (amount === undefined || amount === null) return '$0.00';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
}

export function formatDate(iso: string): string {
  if (!iso) return '';
  try {
    const d = new Date(iso);
    if (isNaN(d.getTime())) return iso;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return iso;
  }
}
