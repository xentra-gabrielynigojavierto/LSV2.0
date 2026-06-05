import { auditApi } from './audit.api';
import { mapToTimelineItem, mapTimelinePagination } from './audit.mapper';
import type { AuditEntityType, AuditQuery, TimelineItem, TimelinePagination } from './audit.types';

export interface TimelineResult {
  items: TimelineItem[];
  pagination: TimelinePagination;
}

export const auditService = {
  async getEntityTimeline(
    entityType: AuditEntityType,
    entityId: string,
    query: AuditQuery = {},
  ): Promise<TimelineResult> {
    const { data: wrapper } = await auditApi.getEntityEvents(entityType, entityId, {
      pageSize: 20,
      sortDescending: true,
      ...query,
    });
    const payload = wrapper.data;
    return {
      items: payload.items.map(mapToTimelineItem),
      pagination: mapTimelinePagination(payload),
    };
  },

  async getCaseTimeline(caseId: string, query: AuditQuery = {}): Promise<TimelineResult> {
    return auditService.getEntityTimeline('Case', caseId, query);
  },

  async getLienTimeline(lienId: string, query: AuditQuery = {}): Promise<TimelineResult> {
    return auditService.getEntityTimeline('Lien', lienId, query);
  },

  async getServicingTimeline(servicingId: string, query: AuditQuery = {}): Promise<TimelineResult> {
    return auditService.getEntityTimeline('ServicingItem', servicingId, query);
  },

  async getBillOfSaleTimeline(bosId: string, query: AuditQuery = {}): Promise<TimelineResult> {
    return auditService.getEntityTimeline('BillOfSale', bosId, query);
  },

  async getContactTimeline(contactId: string, query: AuditQuery = {}): Promise<TimelineResult> {
    return auditService.getEntityTimeline('Contact', contactId, query);
  },

  async getDocumentTimeline(documentId: string, query: AuditQuery = {}): Promise<TimelineResult> {
    return auditService.getEntityTimeline('Document', documentId, query);
  },
};
