import { billOfSaleApi } from './billofsale.api';
import {
  mapBosToListItem,
  mapBosToDetail,
  mapBosPagination,
} from './billofsale.mapper';
import type {
  BillOfSaleQuery,
  BillOfSaleListItem,
  BillOfSaleDetail,
  PaginationMeta,
} from './billofsale.types';

export interface BillOfSaleListResult {
  items: BillOfSaleListItem[];
  pagination: PaginationMeta;
}

export const billOfSaleService = {
  async getBillOfSales(query: BillOfSaleQuery = {}): Promise<BillOfSaleListResult> {
    const { data } = await billOfSaleApi.list(query);
    return {
      items: data.items.map(mapBosToListItem),
      pagination: mapBosPagination(data),
    };
  },

  async getBillOfSale(id: string): Promise<BillOfSaleDetail> {
    const { data } = await billOfSaleApi.getById(id);
    return mapBosToDetail(data);
  },

  async getBillOfSaleByNumber(bosNumber: string): Promise<BillOfSaleDetail> {
    const { data } = await billOfSaleApi.getByNumber(bosNumber);
    return mapBosToDetail(data);
  },

  async getBillOfSalesByLien(lienId: string): Promise<BillOfSaleListItem[]> {
    const { data } = await billOfSaleApi.getByLienId(lienId);
    return data.map(mapBosToListItem);
  },

  async submitForExecution(id: string): Promise<BillOfSaleDetail> {
    const { data } = await billOfSaleApi.submitForExecution(id);
    return mapBosToDetail(data);
  },

  async execute(id: string): Promise<BillOfSaleDetail> {
    const { data } = await billOfSaleApi.execute(id);
    return mapBosToDetail(data);
  },

  async cancel(id: string, reason?: string): Promise<BillOfSaleDetail> {
    const { data } = await billOfSaleApi.cancel(id, reason);
    return mapBosToDetail(data);
  },

  getDocumentUrl(id: string): string {
    return billOfSaleApi.getDocumentUrl(id);
  },
};
