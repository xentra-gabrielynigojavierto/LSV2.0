import type { AuthProvider }         from '@/domain/interfaces/auth-provider';
import { JwtAuthProvider, MockAuthProvider } from './jwt-auth-provider';
import { config }                            from '@/shared/config';
import { logger }                            from '@/shared/logger';

let _instance: AuthProvider | null = null;

export function getAuthProvider(): AuthProvider {
  if (_instance) return _instance;

  if (config.AUTH_PROVIDER === 'mock') {
    if (config.NODE_ENV === 'production') {
      throw new Error('MockAuthProvider MUST NOT be used in production');
    }
    _instance = new MockAuthProvider();
  } else {
    _instance = new JwtAuthProvider();
  }

  logger.info({ provider: _instance.providerName() }, 'Auth provider initialised');
  return _instance;
}
