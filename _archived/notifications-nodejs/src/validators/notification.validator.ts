import { NotificationChannel, NotificationChannels } from "../types";

export interface EmailRecipientInput { email: string }
export interface SmsRecipientInput { phoneNumber: string }
export interface EmailMessageInput { subject: string; body: string; html?: string }
export interface SmsMessageInput { body: string }

export interface SubmitNotificationInput {
  channel?: unknown;
  recipient?: unknown;
  message?: unknown;
  idempotencyKey?: unknown;
  metadata?: unknown;
  templateKey?: unknown;
  templateData?: unknown;
}

export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE_REGEX = /^\+?[1-9]\d{6,14}$/;
const IDEMPOTENCY_KEY_REGEX = /^[a-zA-Z0-9_\-]{1,128}$/;

function isString(v: unknown): v is string {
  return typeof v === "string";
}

function isObject(v: unknown): v is Record<string, unknown> {
  return typeof v === "object" && v !== null && !Array.isArray(v);
}

export function validateSubmitNotification(input: SubmitNotificationInput): ValidationResult {
  const errors: string[] = [];

  // Channel
  if (!input.channel) {
    errors.push("channel is required");
  } else if (!isString(input.channel) || !NotificationChannels.includes(input.channel as NotificationChannel)) {
    errors.push(`channel must be one of: ${NotificationChannels.join(", ")}`);
  }

  const channel = input.channel as NotificationChannel | undefined;

  // Mode detection: template vs raw
  const hasMessage = input.message !== undefined && input.message !== null;
  const hasTemplateKey = input.templateKey !== undefined && input.templateKey !== null;

  if (hasMessage && hasTemplateKey) {
    errors.push("message and templateKey are mutually exclusive — provide one or the other");
  } else if (!hasMessage && !hasTemplateKey) {
    errors.push("Either message or templateKey is required");
  }

  // Recipient
  if (!isObject(input.recipient)) {
    errors.push("recipient is required and must be an object");
  } else {
    if (channel === "email") {
      const r = input.recipient as Record<string, unknown>;
      if (!r["email"] || !isString(r["email"])) {
        errors.push("recipient.email is required for email channel");
      } else if (!EMAIL_REGEX.test(r["email"])) {
        errors.push("recipient.email must be a valid email address");
      }
    } else if (channel === "sms") {
      const r = input.recipient as Record<string, unknown>;
      if (!r["phoneNumber"] || !isString(r["phoneNumber"])) {
        errors.push("recipient.phoneNumber is required for sms channel");
      } else if (!PHONE_REGEX.test(r["phoneNumber"])) {
        errors.push("recipient.phoneNumber must be a valid E.164 phone number (e.g. +15551234567)");
      }
    }
  }

  if (hasTemplateKey) {
    // Template mode validation
    if (!isString(input.templateKey) || (input.templateKey as string).trim().length === 0) {
      errors.push("templateKey must be a non-empty string");
    }
    if (input.templateData !== undefined && !isObject(input.templateData)) {
      errors.push("templateData must be an object if provided");
    }
  } else if (hasMessage) {
    // Raw message validation
    if (!isObject(input.message)) {
      errors.push("message must be an object");
    } else {
      const m = input.message as Record<string, unknown>;
      if (channel === "email") {
        if (!m["subject"] || !isString(m["subject"]) || m["subject"].trim().length === 0) {
          errors.push("message.subject is required for email channel");
        }
        if (!m["body"] || !isString(m["body"]) || m["body"].trim().length === 0) {
          errors.push("message.body is required for email channel");
        }
      } else if (channel === "sms") {
        if (!m["body"] || !isString(m["body"]) || m["body"].trim().length === 0) {
          errors.push("message.body is required for sms channel");
        } else if ((m["body"] as string).length > 1600) {
          errors.push("message.body must not exceed 1600 characters for sms channel");
        }
      }
    }
  }

  // Idempotency key (optional)
  if (input.idempotencyKey !== undefined && input.idempotencyKey !== null) {
    if (!isString(input.idempotencyKey)) {
      errors.push("idempotencyKey must be a string if provided");
    } else if (!IDEMPOTENCY_KEY_REGEX.test(input.idempotencyKey)) {
      errors.push("idempotencyKey must be 1-128 alphanumeric characters, hyphens, or underscores");
    }
  }

  return { valid: errors.length === 0, errors };
}
