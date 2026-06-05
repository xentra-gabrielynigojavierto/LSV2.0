import type { SecretsProvider } from '@/domain/interfaces/secrets-provider';

/**
 * EnvSecretsProvider — reads secrets from environment variables.
 * Default for local dev / CI. Production should use aws-sm or gcp-sm.
 */
export class EnvSecretsProvider implements SecretsProvider {
  async getSecret(name: string): Promise<string> {
    const value = process.env[name];
    if (!value) throw new Error(`Secret not found in environment: ${name}`);
    return value;
  }

  providerName(): string {
    return 'env';
  }
}

/**
 * Scaffold stubs — implement by wiring the relevant SDK.
 */
export class AwsSmSecretsProvider implements SecretsProvider {
  async getSecret(_name: string): Promise<string> {
    // TODO: const { SecretsManagerClient, GetSecretValueCommand } = await import('@aws-sdk/client-secrets-manager');
    throw new Error('AwsSmSecretsProvider not yet implemented');
  }
  providerName(): string { return 'aws-sm'; }
}

export class GcpSmSecretsProvider implements SecretsProvider {
  async getSecret(_name: string): Promise<string> {
    // TODO: const { SecretManagerServiceClient } = await import('@google-cloud/secret-manager');
    throw new Error('GcpSmSecretsProvider not yet implemented');
  }
  providerName(): string { return 'gcp-sm'; }
}
