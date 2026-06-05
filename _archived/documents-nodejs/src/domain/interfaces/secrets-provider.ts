/**
 * SecretsProvider — pluggable secrets retrieval abstraction.
 * Concrete implementations: EnvSecretsProvider, AwsSmSecretsProvider, GcpSmSecretsProvider.
 */
export interface SecretsProvider {
  /** Retrieve a secret by name. Throws if not found. */
  getSecret(name: string): Promise<string>;

  /** Return the name of the active provider for observability. */
  providerName(): string;
}
