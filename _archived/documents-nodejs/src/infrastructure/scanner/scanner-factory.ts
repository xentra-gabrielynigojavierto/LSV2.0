import type { FileScannerProvider }     from '@/domain/interfaces/file-scanner-provider';
import { MockFileScannerProvider }      from './mock-file-scanner-provider';
import { ClamAvFileScannerProvider,
         NullFileScannerProvider }      from './clamav-file-scanner-provider';
import { config }                       from '@/shared/config';
import { logger }                       from '@/shared/logger';

let _instance: FileScannerProvider | null = null;

export function getFileScannerProvider(): FileScannerProvider {
  if (_instance) return _instance;

  switch (config.FILE_SCANNER_PROVIDER) {
    case 'mock':
      _instance = new MockFileScannerProvider();
      break;
    case 'clamav':
      _instance = new ClamAvFileScannerProvider();
      break;
    case 'none':
    default:
      _instance = new NullFileScannerProvider();
      break;
  }

  logger.info({ provider: _instance.providerName() }, 'File scanner provider initialised');
  return _instance;
}
