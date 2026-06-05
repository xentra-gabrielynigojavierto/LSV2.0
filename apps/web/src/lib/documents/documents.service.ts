import { documentsApi } from './documents.api';
import {
  mapDocumentToListItem,
  mapDocumentToDetail,
  mapDocumentVersion,
  mapDocumentPagination,
} from './documents.mapper';
import type {
  DocumentsQuery,
  DocumentListItem,
  DocumentDetail,
  DocumentVersion,
  PaginationMeta,
  UpdateDocumentRequestDto,
  UploadDocumentParams,
} from './documents.types';

export interface DocumentListResult {
  items: DocumentListItem[];
  pagination: PaginationMeta;
}

export const documentsService = {
  async list(query: DocumentsQuery = {}): Promise<DocumentListResult> {
    const { data } = await documentsApi.list(query);
    return {
      items: data.data.map(mapDocumentToListItem),
      pagination: mapDocumentPagination(data),
    };
  },

  async getById(id: string): Promise<DocumentDetail> {
    const { data } = await documentsApi.getById(id);
    return mapDocumentToDetail(data.data);
  },

  async upload(params: UploadDocumentParams): Promise<DocumentDetail> {
    const { data } = await documentsApi.upload(params);
    return mapDocumentToDetail(data);
  },

  async update(id: string, request: UpdateDocumentRequestDto): Promise<DocumentDetail> {
    const { data } = await documentsApi.update(id, request);
    return mapDocumentToDetail(data.data);
  },

  async delete(id: string): Promise<void> {
    await documentsApi.delete(id);
  },

  async getViewUrl(id: string): Promise<string> {
    const { data } = await documentsApi.requestViewUrl(id);
    const redeem = data.data.redeemUrl.replace(/^\/+/, '');
    return `/api/documents/${redeem}`;
  },

  async getDownloadUrl(id: string): Promise<string> {
    const { data } = await documentsApi.requestDownloadUrl(id);
    const redeem = data.data.redeemUrl.replace(/^\/+/, '');
    return `/api/documents/${redeem}`;
  },

  async listVersions(id: string): Promise<DocumentVersion[]> {
    const { data } = await documentsApi.listVersions(id);
    return data.data.map(mapDocumentVersion);
  },
};
