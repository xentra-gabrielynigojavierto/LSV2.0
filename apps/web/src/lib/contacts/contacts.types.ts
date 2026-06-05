export interface ContactResponseDto {
  id: string;
  contactType: string;
  firstName: string;
  lastName: string;
  displayName: string;
  title?: string | null;
  organization?: string | null;
  email?: string | null;
  phone?: string | null;
  fax?: string | null;
  website?: string | null;
  addressLine1?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  notes?: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PaginatedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateContactRequestDto {
  contactType: string;
  firstName: string;
  lastName: string;
  title?: string;
  organization?: string;
  email?: string;
  phone?: string;
  fax?: string;
  website?: string;
  addressLine1?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  notes?: string;
}

export interface UpdateContactRequestDto {
  contactType: string;
  firstName: string;
  lastName: string;
  title?: string;
  organization?: string;
  email?: string;
  phone?: string;
  fax?: string;
  website?: string;
  addressLine1?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  notes?: string;
}

export interface ContactsQuery {
  search?: string;
  contactType?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ContactListItem {
  id: string;
  contactType: string;
  displayName: string;
  organization: string;
  email: string;
  phone: string;
  city: string;
  state: string;
  isActive: boolean;
  createdAt: string;
}

export interface ContactDetail extends ContactListItem {
  firstName: string;
  lastName: string;
  title: string;
  fax: string;
  website: string;
  addressLine1: string;
  postalCode: string;
  notes: string;
  updatedAt: string;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
