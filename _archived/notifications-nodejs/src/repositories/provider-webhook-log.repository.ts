import { ProviderWebhookLog, WebhookProcessingStatus } from "../models/provider-webhook-log.model";

interface CreateWebhookLogInput {
  tenantId?: string | null;
  provider: string;
  channel?: string | null;
  requestHeadersJson: string;
  payloadJson: string;
  signatureVerified: boolean;
  processingStatus: WebhookProcessingStatus;
  receivedAt: Date;
}

export class ProviderWebhookLogRepository {
  async create(input: CreateWebhookLogInput): Promise<ProviderWebhookLog> {
    return ProviderWebhookLog.create({
      ...input,
      tenantId: input.tenantId ?? null,
      channel: input.channel ?? null,
    });
  }

  async updateStatus(
    id: string,
    status: WebhookProcessingStatus,
    error?: string | null
  ): Promise<void> {
    await ProviderWebhookLog.update(
      { processingStatus: status, processingError: error ?? null },
      { where: { id } }
    );
  }

  async findById(id: string): Promise<ProviderWebhookLog | null> {
    return ProviderWebhookLog.findByPk(id);
  }
}
