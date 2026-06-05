import type {
  CaseResponseDto,
  CaseListItem,
  CaseDetail,
  CaseLienItem,
  LienResponseDto,
  PaginatedResultDto,
  PaginationMeta,
  UpdateCaseRequestDto,
} from './cases.types';

const CASE_STATUS_LABELS: Record<string, string> = {
  PreDemand: 'Pre-Demand',
  DemandSent: 'Demand Sent',
  InNegotiation: 'In Negotiation',
  CaseSettled: 'Case Settled',
  Closed: 'Closed',
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

export function mapCaseToListItem(dto: CaseResponseDto): CaseListItem {
  return {
    id: dto.id,
    caseNumber: dto.caseNumber,
    clientName: dto.clientDisplayName || `${dto.clientFirstName} ${dto.clientLastName}`.trim(),
    title: safeString(dto.title || dto.externalReference),
    status: dto.status,
    statusLabel: CASE_STATUS_LABELS[dto.status] ?? dto.status,
    lawFirm: safeString((dto as any).lawFirm),
    caseManager: safeString((dto as any).caseManager),
    accidentType: safeString((dto as any).accidentType),
    dateOfIncident: formatDateField(dto.dateOfIncident),
    clientDob: formatDateField(dto.clientDob),
    insuranceCarrier: safeString(dto.insuranceCarrier),
    demandAmount: dto.demandAmount ?? null,
    settlementAmount: dto.settlementAmount ?? null,
    createdAt: formatDateField(dto.createdAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
  };
}

export function mapCaseToDetail(dto: CaseResponseDto): CaseDetail {
  return {
    id: dto.id,
    caseNumber: dto.caseNumber,
    externalReference: safeString(dto.externalReference),
    title: safeString(dto.title),
    clientName: dto.clientDisplayName || `${dto.clientFirstName} ${dto.clientLastName}`.trim(),
    clientFirstName: dto.clientFirstName,
    clientLastName: dto.clientLastName,
    status: dto.status,
    statusLabel: CASE_STATUS_LABELS[dto.status] ?? dto.status,
    dateOfIncident: formatDateField(dto.dateOfIncident),
    clientDob: formatDateField(dto.clientDob),
    clientPhone: safeString(dto.clientPhone),
    clientEmail: safeString(dto.clientEmail),
    clientAddress: safeString(dto.clientAddress),
    insuranceCarrier: safeString(dto.insuranceCarrier),
    policyNumber: safeString(dto.policyNumber),
    claimNumber: safeString(dto.claimNumber),
    demandAmount: dto.demandAmount ?? null,
    settlementAmount: dto.settlementAmount ?? null,
    description: safeString(dto.description),
    notes: safeString(dto.notes),
    openedAt: formatDateField(dto.openedAtUtc),
    closedAt: formatDateField(dto.closedAtUtc),
    createdAt: formatDateField(dto.createdAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
  };
}

export function mapLienToListItem(dto: LienResponseDto): CaseLienItem {
  return {
    id: dto.id,
    lienNumber: dto.lienNumber,
    lienType: dto.lienType,
    status: dto.status,
    originalAmount: dto.originalAmount,
  };
}

export function mapDtoToUpdateRequest(dto: CaseResponseDto): UpdateCaseRequestDto {
  return {
    clientFirstName: dto.clientFirstName,
    clientLastName: dto.clientLastName,
    externalReference: dto.externalReference ?? undefined,
    title: dto.title ?? undefined,
    clientDob: dto.clientDob ?? undefined,
    clientPhone: dto.clientPhone ?? undefined,
    clientEmail: dto.clientEmail ?? undefined,
    clientAddress: dto.clientAddress ?? undefined,
    dateOfIncident: dto.dateOfIncident ?? undefined,
    insuranceCarrier: dto.insuranceCarrier ?? undefined,
    policyNumber: dto.policyNumber ?? undefined,
    claimNumber: dto.claimNumber ?? undefined,
    description: dto.description ?? undefined,
    notes: dto.notes ?? undefined,
    status: dto.status,
    demandAmount: dto.demandAmount ?? undefined,
    settlementAmount: dto.settlementAmount ?? undefined,
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
