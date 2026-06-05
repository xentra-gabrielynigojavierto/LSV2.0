import type { StorageProvider } from '@/domain/interfaces/storage-provider';
import { S3StorageProvider }     from './s3-storage-provider';
import { LocalStorageProvider }  from './local-storage-provider';
import { GCSStorageProvider }    from './gcs-storage-provider';
import { config }                from '@/shared/config';
import { logger }                from '@/shared/logger';

let _instance: StorageProvider | null = null;

export function getStorageProvider(): StorageProvider {
  if (_instance) return _instance;

  switch (config.STORAGE_PROVIDER) {
    case 's3':
      _instance = new S3StorageProvider();
      break;
    case 'gcs':
      _instance = new GCSStorageProvider();
      break;
    case 'local':
    default:
      _instance = new LocalStorageProvider();
      break;
  }

  logger.info({ provider: _instance.providerName() }, 'Storage provider initialised');
  return _instance;
}
