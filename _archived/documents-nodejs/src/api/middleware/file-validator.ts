import multer from 'multer';
import type { Request } from 'express';
import { fromBuffer as fileTypeFromBuffer } from 'file-type';
import { ALLOWED_MIME_TYPES } from '@/shared/constants';
import {
  FileValidationError,
  FileTooLargeError,
  UnsupportedFileTypeError,
} from '@/shared/errors';
import { config } from '@/shared/config';

const MAX_BYTES = config.MAX_FILE_SIZE_MB * 1024 * 1024;

/** Multer config — stores in memory for server-side processing */
export const upload = multer({
  storage: multer.memoryStorage(),
  limits:  { fileSize: MAX_BYTES },
  fileFilter: (_req: Request, file, cb) => {
    if (!ALLOWED_MIME_TYPES[file.mimetype]) {
      cb(new UnsupportedFileTypeError(file.mimetype));
    } else {
      cb(null, true);
    }
  },
}).single('file');

/**
 * Deep file validation: re-check actual magic bytes against declared MIME.
 * Prevents MIME-spoofing attacks (e.g. executable with .pdf extension).
 */
export async function validateFileContent(buffer: Buffer, declaredMime: string): Promise<void> {
  if (buffer.byteLength === 0) {
    throw new FileValidationError('Uploaded file is empty');
  }

  if (buffer.byteLength > MAX_BYTES) {
    throw new FileTooLargeError(config.MAX_FILE_SIZE_MB);
  }

  const detected = await fileTypeFromBuffer(buffer);

  // If file-type can't detect (e.g. plain text/csv), allow if declared type is whitelisted
  if (!detected) {
    if (!ALLOWED_MIME_TYPES[declaredMime]) {
      throw new UnsupportedFileTypeError(declaredMime);
    }
    return;
  }

  // Detect MIME ↔ extension mismatch
  if (detected.mime !== declaredMime) {
    // Allow common aliases (e.g. jpg vs jpeg)
    const detectedExt  = detected.ext;
    const expectedExt  = ALLOWED_MIME_TYPES[declaredMime];
    const detectedExtStr = detectedExt as string;
    const isAlias        = (detectedExtStr === 'jpg' && expectedExt === 'jpeg') ||
                           (detectedExtStr === 'jpeg' && expectedExt === 'jpg');

    if (!isAlias) {
      throw new FileValidationError(
        `MIME mismatch: declared ${declaredMime}, detected ${detected.mime}`,
      );
    }
  }
}
