import fs from 'fs/promises';
import fsSync from 'fs';
import path from 'path';
import crypto from 'crypto';
import type {
  StorageProvider,
  UploadOptions,
  UploadResult,
  SignedUrlOptions,
  DeleteOptions,
} from '@/domain/interfaces/storage-provider';
import { StorageError, NotFoundError } from '@/shared/errors';
import { config } from '@/shared/config';
import { logger } from '@/shared/logger';

/**
 * LocalStorageProvider — filesystem-based implementation for dev/test.
 * Signed URLs are token-protected download paths served by the service itself.
 * NOT suitable for production.
 */
export class LocalStorageProvider implements StorageProvider {
  private readonly basePath: string;
  /** In-memory signed token store — production would use Redis or DB */
  private readonly tokens = new Map<string, { key: string; bucket: string; expiresAt: number }>();

  constructor() {
    this.basePath = path.resolve(config.LOCAL_STORAGE_PATH);
    // Ensure root directory exists
    if (!fsSync.existsSync(this.basePath)) {
      fsSync.mkdirSync(this.basePath, { recursive: true });
    }
  }

  private filePath(bucket: string, key: string): string {
    // Sanitise bucket and key to prevent path traversal
    const safeBucket = path.basename(bucket);
    const safeKey    = key.replace(/\.\./g, '_');
    return path.join(this.basePath, safeBucket, safeKey);
  }

  async upload(options: UploadOptions): Promise<UploadResult> {
    try {
      const fp  = this.filePath(options.bucket, options.key);
      const dir = path.dirname(fp);
      await fs.mkdir(dir, { recursive: true });
      await fs.writeFile(fp, options.body);
      logger.debug({ key: options.key, bucket: options.bucket }, 'Local file uploaded');
      return { key: options.key, bucket: options.bucket };
    } catch (err) {
      throw new StorageError(`Local upload failed: ${(err as Error).message}`);
    }
  }

  async generateSignedUrl(options: SignedUrlOptions): Promise<string> {
    const token      = crypto.randomBytes(32).toString('hex');
    const expiresAt  = Date.now() + options.expiresInSeconds * 1000;
    this.tokens.set(token, { key: options.key, bucket: options.bucket, expiresAt });
    // URL routed through the service's own /internal/files endpoint
    return `/internal/files?token=${token}`;
  }

  /**
   * Resolve a local signed token back to file content.
   * Called by the /internal/files route — never exposed externally without token validation.
   */
  async resolveToken(token: string): Promise<{ buffer: Buffer; key: string; bucket: string }> {
    const entry = this.tokens.get(token);
    if (!entry) throw new NotFoundError('Token', token);
    if (Date.now() > entry.expiresAt) {
      this.tokens.delete(token);
      throw new NotFoundError('Token', token);
    }
    const fp     = this.filePath(entry.bucket, entry.key);
    const buffer = await fs.readFile(fp);
    return { buffer, key: entry.key, bucket: entry.bucket };
  }

  async delete(options: DeleteOptions): Promise<void> {
    try {
      await fs.unlink(this.filePath(options.bucket, options.key));
    } catch {
      // Ignore not-found on delete
    }
  }

  async exists(bucket: string, key: string): Promise<boolean> {
    try {
      await fs.access(this.filePath(bucket, key));
      return true;
    } catch {
      return false;
    }
  }

  providerName(): string {
    return 'local';
  }
}
