import { casesApi } from './cases.api';
import {
  mapCaseToListItem,
  mapCaseToDetail,
  mapLienToListItem,
  mapPagination,
  mapDtoToUpdateRequest,
} from './cases.mapper';
import type {
  CasesQuery,
  CaseListItem,
  CaseDetail,
  CaseLienItem,
  PaginationMeta,
  CreateCaseRequestDto,
  UpdateCaseRequestDto,
} from './cases.types';

export interface CaseListResult {
  items: CaseListItem[];
  pagination: PaginationMeta;
}

export interface CaseLiensResult {
  items: CaseLienItem[];
  pagination: PaginationMeta;
}

export const casesService = {
  async getCases(query: CasesQuery = {}): Promise<CaseListResult> {
    const { data } = await casesApi.list(query);
    return {
      items: data.items.map(mapCaseToListItem),
      pagination: mapPagination(data),
    };
  },

  async getCase(caseId: string): Promise<CaseDetail> {
    const { data } = await casesApi.getById(caseId);
    return mapCaseToDetail(data);
  },

  async createCase(request: CreateCaseRequestDto): Promise<CaseDetail> {
    const { data } = await casesApi.create(request);
    return mapCaseToDetail(data);
  },

  async updateCase(caseId: string, request: UpdateCaseRequestDto): Promise<CaseDetail> {
    const { data } = await casesApi.update(caseId, request);
    return mapCaseToDetail(data);
  },

  async updateCaseStatus(caseId: string, newStatus: string): Promise<CaseDetail> {
    const { data: freshDto } = await casesApi.getById(caseId);
    const request = mapDtoToUpdateRequest(freshDto);
    request.status = newStatus;
    return this.updateCase(caseId, request);
  },

  async getCaseLiens(caseId: string): Promise<CaseLiensResult> {
    const { data } = await casesApi.listLiensByCase(caseId);
    return {
      items: data.items.map(mapLienToListItem),
      pagination: mapPagination(data),
    };
  },
};
