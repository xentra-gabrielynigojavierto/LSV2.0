import { liensApi } from './liens.api';
import {
  mapLienToListItem,
  mapLienToDetail,
  mapOfferToItem,
  mapPagination,
} from './liens.mapper';
import type {
  LiensQuery,
  LienListItem,
  LienDetail,
  LienOfferItem,
  PaginationMeta,
  CreateLienRequestDto,
  UpdateLienRequestDto,
  CreateLienOfferRequestDto,
  SaleFinalizationResultDto,
} from './liens.types';

export interface LienListResult {
  items: LienListItem[];
  pagination: PaginationMeta;
}

export interface LienOffersResult {
  items: LienOfferItem[];
}

export const liensService = {
  async getLiens(query: LiensQuery = {}): Promise<LienListResult> {
    const { data } = await liensApi.list(query);
    return {
      items: data.items.map(mapLienToListItem),
      pagination: mapPagination(data),
    };
  },

  async getLien(lienId: string): Promise<LienDetail> {
    const { data } = await liensApi.getById(lienId);
    return mapLienToDetail(data);
  },

  async createLien(request: CreateLienRequestDto): Promise<LienDetail> {
    const { data } = await liensApi.create(request);
    return mapLienToDetail(data);
  },

  async updateLien(id: string, request: UpdateLienRequestDto): Promise<LienDetail> {
    const { data } = await liensApi.update(id, request);
    return mapLienToDetail(data);
  },

  async withdraw(id: string): Promise<LienDetail> {
    const { data } = await liensApi.withdraw(id);
    return mapLienToDetail(data);
  },

  async getLienOffers(lienId: string): Promise<LienOffersResult> {
    const { data } = await liensApi.getOffers(lienId);
    return {
      items: data.map(mapOfferToItem),
    };
  },

  async createOffer(request: CreateLienOfferRequestDto): Promise<LienOfferItem> {
    const { data } = await liensApi.createOffer(request);
    return mapOfferToItem(data);
  },

  async acceptOffer(offerId: string): Promise<SaleFinalizationResultDto> {
    const { data } = await liensApi.acceptOffer(offerId);
    return data;
  },
};
