/**
 * StorageProvider — cloud-agnostic file storage abstraction.
 * Concrete implementations: S3StorageProvider, LocalStorageProvider, GCSStorageProvider.
 * NO AWS / GCP SDK may be used outside the infrastructure/storage layer.
 */
export interface UploadOptions {
  bucket:       string;
  key:          string;
  body:         Buffer;
  mimeType:     string;
  metadata?:    Record<string, string>;
  /** Server-side encryption algorithm, e.g. 'AES256' or 'aws:kms' */
  serverSideEncryption?: string;
}

export interface UploadResult {
  key:      string;
  bucket:   string;
  eTag?:    string;
  location?: string;
}

export interface SignedUrlOptions {
  bucket:         string;
  key:            string;
  /** Seconds until the URL expires */
  expiresInSeconds: number;
  /** 'GET' for view/download, 'PUT' for upload */
  operation:      'GET' | 'PUT';
  contentType?:   string;
}

export interface DeleteOptions {
  bucket: string;
  key:    string;
}

export interface StorageProvider {
  /** Upload a file. Returns the storage reference. */
  upload(options: UploadOptions): Promise<UploadResult>;

  /** Generate a short-lived signed URL for secure access. */
  generateSignedUrl(options: SignedUrlOptions): Promise<string>;

  /** Soft-delete (or hard-delete) a file from storage. */
  delete(options: DeleteOptions): Promise<void>;

  /** Check if an object exists. */
  exists(bucket: string, key: string): Promise<boolean>;

  /** Return the name of the active provider for observability. */
  providerName(): string;
}
