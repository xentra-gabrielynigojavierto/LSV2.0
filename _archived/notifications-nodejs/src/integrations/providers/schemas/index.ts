import { SENDGRID_CATALOG } from "./sendgrid.schema";
import { SMTP_CATALOG } from "./smtp.schema";
import { TWILIO_CATALOG } from "./twilio.schema";

export { SENDGRID_CATALOG, SMTP_CATALOG, TWILIO_CATALOG };

export const PROVIDER_CATALOG = [SENDGRID_CATALOG, SMTP_CATALOG, TWILIO_CATALOG];

export const SUPPORTED_EMAIL_PROVIDERS = ["sendgrid", "smtp"] as const;
export const SUPPORTED_SMS_PROVIDERS = ["twilio"] as const;
export type SupportedEmailProvider = typeof SUPPORTED_EMAIL_PROVIDERS[number];
export type SupportedSmsProvider = typeof SUPPORTED_SMS_PROVIDERS[number];

export function isSupportedEmailProvider(p: string): p is SupportedEmailProvider {
  return SUPPORTED_EMAIL_PROVIDERS.includes(p as SupportedEmailProvider);
}

export function isSupportedSmsProvider(p: string): p is SupportedSmsProvider {
  return SUPPORTED_SMS_PROVIDERS.includes(p as SupportedSmsProvider);
}
