import { NormalizedEventType } from "../../../models/notification-event.model";

export interface TwilioStatusPayload {
  MessageSid?: string;
  SmsSid?: string;
  SmsStatus?: string;
  MessageStatus?: string;
  To?: string;
  From?: string;
  ErrorCode?: string;
  ErrorMessage?: string;
  AccountSid?: string;
  [key: string]: string | undefined;
}

export interface NormalizedProviderEvent {
  rawEventType: string;
  normalizedEventType: NormalizedEventType;
  providerMessageId: string | null;
  eventTimestamp: Date;
  recipientPhone: string | null;
  errorCode: string | null;
  errorMessage: string | null;
  metadata: Record<string, unknown>;
}

const TWILIO_STATUS_MAP: Record<string, NormalizedEventType> = {
  queued: "queued",
  accepted: "accepted",
  sending: "queued",
  sent: "sent",
  receiving: "queued",
  received: "delivered",
  delivered: "delivered",
  undelivered: "undeliverable",
  failed: "failed",
  canceled: "failed",
  scheduled: "queued",
  read: "opened",
};

export function normalizeTwilioEvent(raw: TwilioStatusPayload): NormalizedProviderEvent {
  const rawStatus = (raw.MessageStatus || raw.SmsStatus || "unknown").toLowerCase();
  const normalizedEventType: NormalizedEventType = TWILIO_STATUS_MAP[rawStatus] ?? "failed";

  const messageSid = raw.MessageSid || raw.SmsSid || null;

  const { MessageSid, SmsSid, SmsStatus, MessageStatus, ...rest } = raw;

  return {
    rawEventType: rawStatus,
    normalizedEventType,
    providerMessageId: messageSid ?? null,
    eventTimestamp: new Date(),
    recipientPhone: raw.To ?? null,
    errorCode: raw.ErrorCode ?? null,
    errorMessage: raw.ErrorMessage ?? null,
    metadata: rest,
  };
}
