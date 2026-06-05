import type {
  LienResponseDto,
  LienOfferResponseDto,
  LienListItem,
  LienDetail,
  LienOfferItem,
  PaginatedResultDto,
  PaginationMeta,
  UpdateLienRequestDto,
} from './liens.types';

const LIEN_TYPE_LABELS: Record<string, string> = {
  MedicalLien: 'Medical Lien',
  AttorneyLien: 'Attorney Lien',
  SettlementAdvance: 'Settlement Advance',
  WorkersCompLien: "Workers' Comp Lien",
  PropertyLien: 'Property Lien',
  Other: 'Other',
};

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

function buildSubjectName(dto: LienResponseDto): string {
  if (dto.isConfidential) return 'Confidential';
  if (dto.subjectDisplayName) return dto.subjectDisplayName;
  const parts = [dto.subjectFirstName, dto.subjectLastName].filter(Boolean);
  return parts.length ? parts.join(' ') : '';
}

export function mapLienToListItem(dto: LienResponseDto): LienListItem {
  return {
    id: dto.id,
    lienNumber: dto.lienNumber,
    lienType: dto.lienType,
    lienTypeLabel: LIEN_TYPE_LABELS[dto.lienType] ?? dto.lienType,
    status: dto.status,
    caseId: safeString(dto.caseId),
    originalAmount: dto.originalAmount,
    currentBalance: dto.currentBalance ?? null,
    offerPrice: dto.offerPrice ?? null,
    purchasePrice: dto.purchasePrice ?? null,
    jurisdiction: safeString(dto.jurisdiction),
    isConfidential: dto.isConfidential,
    subjectName: buildSubjectName(dto),
    createdAt: formatDateField(dto.createdAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
  };
}

export function mapLienToDetail(dto: LienResponseDto): LienDetail {
  return {
    id: dto.id,
    lienNumber: dto.lienNumber,
    externalReference: safeString(dto.externalReference),
    lienType: dto.lienType,
    lienTypeLabel: LIEN_TYPE_LABELS[dto.lienType] ?? dto.lienType,
    status: dto.status,
    caseId: safeString(dto.caseId),
    originalAmount: dto.originalAmount,
    currentBalance: dto.currentBalance ?? null,
    offerPrice: dto.offerPrice ?? null,
    purchasePrice: dto.purchasePrice ?? null,
    payoffAmount: dto.payoffAmount ?? null,
    jurisdiction: safeString(dto.jurisdiction),
    isConfidential: dto.isConfidential,
    subjectName: buildSubjectName(dto),
    subjectFirstName: safeString(dto.subjectFirstName),
    subjectLastName: safeString(dto.subjectLastName),
    orgId: dto.orgId,
    sellingOrgId: safeString(dto.sellingOrgId),
    buyingOrgId: safeString(dto.buyingOrgId),
    holdingOrgId: safeString(dto.holdingOrgId),
    incidentDate: formatDateField(dto.incidentDate),
    description: safeString(dto.description),
    openedAt: formatDateField(dto.openedAtUtc),
    closedAt: formatDateField(dto.closedAtUtc),
    createdAt: formatDateField(dto.createdAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
  };
}

export function mapOfferToItem(dto: LienOfferResponseDto): LienOfferItem {
  return {
    id: dto.id,
    lienId: dto.lienId,
    offerAmount: dto.offerAmount,
    status: dto.status,
    buyerOrgId: dto.buyerOrgId,
    sellerOrgId: dto.sellerOrgId,
    notes: safeString(dto.notes),
    responseNotes: safeString(dto.responseNotes),
    offeredAt: formatDateField(dto.offeredAtUtc),
    expiresAt: formatDateField(dto.expiresAtUtc),
    respondedAt: formatDateField(dto.respondedAtUtc),
    isExpired: dto.isExpired,
    createdAt: formatDateField(dto.createdAtUtc),
  };
}

export function mapDtoToUpdateRequest(dto: LienResponseDto): UpdateLienRequestDto {
  return {
    externalReference: dto.externalReference ?? undefined,
    lienType: dto.lienType,
    caseId: dto.caseId ?? undefined,
    facilityId: dto.facilityId ?? undefined,
    originalAmount: dto.originalAmount,
    jurisdiction: dto.jurisdiction ?? undefined,
    isConfidential: dto.isConfidential,
    subjectFirstName: dto.subjectFirstName ?? undefined,
    subjectLastName: dto.subjectLastName ?? undefined,
    incidentDate: dto.incidentDate ?? undefined,
    description: dto.description ?? undefined,
  };
}

export function mapPagination<T>(result: PaginatedResultDto<T>): PaginationMeta {
  return {
    page: result.page,
    pageSize: result.pageSize,
    totalCount: result.totalCount,
    totalPages: Math.ceil(result.totalCount / Math.max(result.pageSize, 1)),
  };
}
