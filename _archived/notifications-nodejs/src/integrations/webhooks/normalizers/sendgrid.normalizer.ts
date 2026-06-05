import { NormalizedEventType } from "../../../models/notification-event.model";

export interface SendGridEventItem {
  event: string;
  sg_message_id?: string;
  timestamp?: number;
  email?: string;
  reason?: string;
  status?: string;
  type?: string;
  url?: string;
  [key: string]: unknown;
}

export interface NormalizedProviderEvent {
  rawEventType: string;
  normalizedEventType: NormalizedEventType;
  providerMessageId: string | null;
  eventTimestamp: Date;
  recipientEmail: string | null;
  metadata: Record<string, unknown>;
}

const SENDGRID_EVENT_MAP: Record<string, NormalizedEventType> = {
  processed: "accepted",
  deferred: "deferred",
  delivered: "delivered",
  bounce: "bounced",
  blocked: "rejected",
  dropped: "rejected",
  open: "opened",
  click: "clicked",
  spamreport: "complained",
  unsubscribe: "unsubscribed",
  group_unsubscribe: "unsubscribed",
  group_resubscribe: "accepted",
  machine_opened: "opened",
};

function extractSgMessageId(raw: SendGridEventItem): string | null {
  const id = raw.sg_message_id;
  if (!id) return null;
  // sg_message_id may have a suffix like ".filter0001p3mdm1-17502-5F4B5765-1.0"
  // The core ID is the first segment
  return id.split(".")[0] ?? id;
}

export function normalizeSendGridEvent(raw: SendGridEventItem): NormalizedProviderEvent {
  const rawEventType = raw.event || "unknown";
  const normalizedEventType: NormalizedEventType =
    SENDGRID_EVENT_MAP[rawEventType] ?? "failed";

  const ts = raw.timestamp ? new Date(raw.timestamp * 1000) : new Date();
  const { event, sg_message_id, timestamp, email, ...rest } = raw;

  return {
    rawEventType,
    normalizedEventType,
    providerMessageId: extractSgMessageId(raw),
    eventTimestamp: ts,
    recipientEmail: raw.email ?? null,
    metadata: rest,
  };
}
