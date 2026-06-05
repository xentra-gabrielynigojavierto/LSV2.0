import type {
  ContactResponseDto,
  PaginatedResultDto,
  ContactListItem,
  ContactDetail,
  PaginationMeta,
} from './contacts.types';

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

export function mapContactToListItem(dto: ContactResponseDto): ContactListItem {
  return {
    id: dto.id,
    contactType: dto.contactType,
    displayName: dto.displayName,
    organization: safeString(dto.organization),
    email: safeString(dto.email),
    phone: safeString(dto.phone),
    city: safeString(dto.city),
    state: safeString(dto.state),
    isActive: dto.isActive,
    createdAt: formatDateField(dto.createdAtUtc),
  };
}

export function mapContactToDetail(dto: ContactResponseDto): ContactDetail {
  return {
    ...mapContactToListItem(dto),
    firstName: dto.firstName,
    lastName: dto.lastName,
    title: safeString(dto.title),
    fax: safeString(dto.fax),
    website: safeString(dto.website),
    addressLine1: safeString(dto.addressLine1),
    postalCode: safeString(dto.postalCode),
    notes: safeString(dto.notes),
    updatedAt: formatDateField(dto.updatedAtUtc),
  };
}

export function mapContactPagination<T>(result: PaginatedResultDto<T>): PaginationMeta {
  return {
    page: result.page,
    pageSize: result.pageSize,
    totalCount: result.totalCount,
    totalPages: Math.ceil(result.totalCount / Math.max(result.pageSize, 1)),
  };
}
