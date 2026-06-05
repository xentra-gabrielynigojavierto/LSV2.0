import { EmailProviderAdapter } from "../interfaces/email-provider.interface";
import { SmsProviderAdapter } from "../interfaces/sms-provider.interface";
import { NotificationChannel } from "../../../types";
import { logger } from "../../../shared/logger";

type ProviderAdapter = EmailProviderAdapter | SmsProviderAdapter;

export class ProviderRegistry {
  private emailProviders: Map<string, EmailProviderAdapter> = new Map();
  private smsProviders: Map<string, SmsProviderAdapter> = new Map();

  registerEmailProvider(provider: EmailProviderAdapter): void {
    this.emailProviders.set(provider.providerType, provider);
    logger.info("Registered email provider", { providerType: provider.providerType });
  }

  registerSmsProvider(provider: SmsProviderAdapter): void {
    this.smsProviders.set(provider.providerType, provider);
    logger.info("Registered SMS provider", { providerType: provider.providerType });
  }

  getEmailProvider(providerType: string): EmailProviderAdapter | undefined {
    return this.emailProviders.get(providerType);
  }

  getSmsProvider(providerType: string): SmsProviderAdapter | undefined {
    return this.smsProviders.get(providerType);
  }

  getRegisteredProviderTypes(channel: NotificationChannel): string[] {
    if (channel === "email") return Array.from(this.emailProviders.keys());
    if (channel === "sms") return Array.from(this.smsProviders.keys());
    return [];
  }

  getAllProviders(): ProviderAdapter[] {
    return [
      ...Array.from(this.emailProviders.values()),
      ...Array.from(this.smsProviders.values()),
    ];
  }
}

export const providerRegistry = new ProviderRegistry();
