import {
  S3Client,
  PutObjectCommand,
  DeleteObjectCommand,
  HeadObjectCommand,
} from '@aws-sdk/client-s3';
import { getSignedUrl } from '@aws-sdk/s3-request-presigner';
import { GetObjectCommand } from '@aws-sdk/client-s3';
import type {
  StorageProvider,
  UploadOptions,
  UploadResult,
  SignedUrlOptions,
  DeleteOptions,
} from '@/domain/interfaces/storage-provider';
import { StorageError } from '@/shared/errors';
import { config } from '@/shared/config';

/**
 * S3StorageProvider — AWS S3 implementation of StorageProvider.
 * All S3 SDK usage is confined to this file.
 * Enforces: private ACL, SSE, no public access.
 */
export class S3StorageProvider implements StorageProvider {
  private readonly client: S3Client;

  constructor() {
    this.client = new S3Client({
      region: config.AWS_REGION ?? 'us-east-1',
      ...(config.AWS_ACCESS_KEY_ID && config.AWS_SECRET_ACCESS_KEY
        ? {
            credentials: {
              accessKeyId:     config.AWS_ACCESS_KEY_ID,
              secretAccessKey: config.AWS_SECRET_ACCESS_KEY,
            },
          }
        : {}),
      // Override endpoint for LocalStack / MinIO
      ...(config.S3_ENDPOINT_URL
        ? { endpoint: config.S3_ENDPOINT_URL, forcePathStyle: true }
        : {}),
    });
  }

  async upload(options: UploadOptions): Promise<UploadResult> {
    try {
      const command = new PutObjectCommand({
        Bucket:               options.bucket,
        Key:                  options.key,
        Body:                 options.body,
        ContentType:          options.mimeType,
        // Enforce no public access
        ACL:                  undefined, // rely on bucket policy — never 'public-read'
        ServerSideEncryption: (options.serverSideEncryption ?? 'AES256') as 'AES256' | 'aws:kms',
        Metadata:             options.metadata ?? {},
      });

      await this.client.send(command);

      return { key: options.key, bucket: options.bucket };
    } catch (err) {
      throw new StorageError(`S3 upload failed: ${(err as Error).message}`);
    }
  }

  async generateSignedUrl(options: SignedUrlOptions): Promise<string> {
    try {
      const command =
        options.operation === 'GET'
          ? new GetObjectCommand({ Bucket: options.bucket, Key: options.key })
          : new PutObjectCommand({
              Bucket:      options.bucket,
              Key:         options.key,
              ContentType: options.contentType,
            });

      return await getSignedUrl(this.client, command, {
        expiresIn: options.expiresInSeconds,
      });
    } catch (err) {
      throw new StorageError(`S3 signed URL generation failed: ${(err as Error).message}`);
    }
  }

  async delete(options: DeleteOptions): Promise<void> {
    try {
      await this.client.send(
        new DeleteObjectCommand({ Bucket: options.bucket, Key: options.key }),
      );
    } catch (err) {
      throw new StorageError(`S3 delete failed: ${(err as Error).message}`);
    }
  }

  async exists(bucket: string, key: string): Promise<boolean> {
    try {
      await this.client.send(new HeadObjectCommand({ Bucket: bucket, Key: key }));
      return true;
    } catch {
      return false;
    }
  }

  providerName(): string {
    return 's3';
  }
}
