import { apiClient } from '@/lib/api-client';
import type {
  DocumentResponseDto,
  DocumentListResponseDto,
  DocumentVersionResponseDto,
  IssuedTokenResponseDto,
  UpdateDocumentRequestDto,
  DocumentsQuery,
  UploadDocumentParams,
} from './documents.types';

const BASE = '/documents/documents';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const documentsApi = {
  list(query: DocumentsQuery = {}) {
    return apiClient.get<DocumentListResponseDto>(
      `${BASE}${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<{ data: DocumentResponseDto }>(`${BASE}/${id}`);
  },

  update(id: string, request: UpdateDocumentRequestDto) {
    return apiClient.patch<{ data: DocumentResponseDto }>(`${BASE}/${id}`, request);
  },

  delete(id: string) {
    return apiClient.delete<void>(`${BASE}/${id}`);
  },

  async upload(params: UploadDocumentParams): Promise<{ data: DocumentResponseDto; correlationId: string }> {
    const formData = new FormData();
    formData.append('file', params.file);
    formData.append('tenantId', params.tenantId);
    formData.append('productId', params.productId);
    formData.append('referenceId', params.referenceId);
    formData.append('referenceType', params.referenceType);
    formData.append('documentTypeId', params.documentTypeId);
    formData.append('title', params.title);
    if (params.description) {
      formData.append('description', params.description);
    }

    const res = await fetch(`/api${BASE}`, {
      method: 'POST',
      credentials: 'include',
      body: formData,
    });

    const correlationId = res.headers.get('X-Correlation-Id') ?? 'unknown';

    if (!res.ok) {
      let message = `HTTP ${res.status}`;
      try {
        const errBody = await res.json();
        message = errBody.message ?? errBody.title ?? message;
      } catch { /* non-JSON error body */ }
      throw new Error(message);
    }

    const body: { data: DocumentResponseDto } = await res.json();
    return { data: body.data, correlationId };
  },

  requestViewUrl(id: string) {
    return apiClient.post<{ data: IssuedTokenResponseDto }>(`${BASE}/${id}/view-url`, {});
  },

  requestDownloadUrl(id: string) {
    return apiClient.post<{ data: IssuedTokenResponseDto }>(`${BASE}/${id}/download-url`, {});
  },

  listVersions(id: string) {
    return apiClient.get<{ data: DocumentVersionResponseDto[] }>(`${BASE}/${id}/versions`);
  },
};
