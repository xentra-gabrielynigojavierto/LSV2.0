import { contactsApi } from './contacts.api';
import {
  mapContactToListItem,
  mapContactToDetail,
  mapContactPagination,
} from './contacts.mapper';
import type {
  ContactsQuery,
  ContactListItem,
  ContactDetail,
  PaginationMeta,
  CreateContactRequestDto,
  UpdateContactRequestDto,
} from './contacts.types';

export interface ContactListResult {
  items: ContactListItem[];
  pagination: PaginationMeta;
}

export const contactsService = {
  async getContacts(query: ContactsQuery = {}): Promise<ContactListResult> {
    const { data } = await contactsApi.list(query);
    return {
      items: data.items.map(mapContactToListItem),
      pagination: mapContactPagination(data),
    };
  },

  async getContact(id: string): Promise<ContactDetail> {
    const { data } = await contactsApi.getById(id);
    return mapContactToDetail(data);
  },

  async createContact(request: CreateContactRequestDto): Promise<ContactDetail> {
    const { data } = await contactsApi.create(request);
    return mapContactToDetail(data);
  },

  async updateContact(id: string, request: UpdateContactRequestDto): Promise<ContactDetail> {
    const { data } = await contactsApi.update(id, request);
    return mapContactToDetail(data);
  },

  async deactivateContact(id: string): Promise<ContactDetail> {
    const { data } = await contactsApi.deactivate(id);
    return mapContactToDetail(data);
  },

  async reactivateContact(id: string): Promise<ContactDetail> {
    const { data } = await contactsApi.reactivate(id);
    return mapContactToDetail(data);
  },
};
