import { servicingApi } from './servicing.api';
import {
  mapServicingToListItem,
  mapServicingToDetail,
  mapServicingPagination,
} from './servicing.mapper';
import type {
  ServicingQuery,
  ServicingListItem,
  ServicingDetail,
  PaginationMeta,
  CreateServicingItemRequestDto,
  UpdateServicingItemRequestDto,
} from './servicing.types';

export interface ServicingListResult {
  items: ServicingListItem[];
  pagination: PaginationMeta;
}

export const servicingService = {
  async getItems(query: ServicingQuery = {}): Promise<ServicingListResult> {
    const { data } = await servicingApi.list(query);
    return {
      items: data.items.map(mapServicingToListItem),
      pagination: mapServicingPagination(data),
    };
  },

  async getItem(id: string): Promise<ServicingDetail> {
    const { data } = await servicingApi.getById(id);
    return mapServicingToDetail(data);
  },

  async createItem(request: CreateServicingItemRequestDto): Promise<ServicingDetail> {
    const { data } = await servicingApi.create(request);
    return mapServicingToDetail(data);
  },

  async updateItem(id: string, request: UpdateServicingItemRequestDto): Promise<ServicingDetail> {
    const { data } = await servicingApi.update(id, request);
    return mapServicingToDetail(data);
  },

  async updateStatus(id: string, status: string, resolution?: string): Promise<ServicingDetail> {
    const { data } = await servicingApi.updateStatus(id, { status, resolution });
    return mapServicingToDetail(data);
  },
};
