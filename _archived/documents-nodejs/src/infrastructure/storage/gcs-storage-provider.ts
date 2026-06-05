import type {
  StorageProvider,
  UploadOptions,
  UploadResult,
  SignedUrlOptions,
  DeleteOptions,
} from '@/domain/interfaces/storage-provider';

/**
 * GCSStorageProvider — Google Cloud Storage scaffold.
 * Implement by installing @google-cloud/storage and wiring config.GCS_* fields.
 * All GCS SDK usage must remain within this file.
 */
export class GCSStorageProvider implements StorageProvider {
  constructor() {
    // TODO: const { Storage } = await import('@google-cloud/storage');
    //       this.client = new Storage({ projectId: config.GCS_PROJECT_ID, keyFilename: config.GCS_KEY_FILE_PATH });
    throw new Error(
      'GCSStorageProvider is not yet implemented. ' +
      'Install @google-cloud/storage and implement this class.',
    );
  }

  async upload(_options: UploadOptions): Promise<UploadResult> {
    throw new Error('Not implemented');
  }

  async generateSignedUrl(_options: SignedUrlOptions): Promise<string> {
    throw new Error('Not implemented');
  }

  async delete(_options: DeleteOptions): Promise<void> {
    throw new Error('Not implemented');
  }

  async exists(_bucket: string, _key: string): Promise<boolean> {
    throw new Error('Not implemented');
  }

  providerName(): string {
    return 'gcs';
  }
}
