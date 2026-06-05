import * as crypto from "crypto";
import { logger } from "./logger";

const ALGORITHM = "aes-256-cbc";
const IV_LENGTH = 16;
const KEY_LENGTH = 32;

function deriveKey(rawKey: string): Buffer {
  // SHA-256 of the raw key produces a deterministic 32-byte key
  return crypto.createHash("sha256").update(rawKey).digest();
}

let encryptionKey: Buffer | null = null;

function getKey(): Buffer {
  if (encryptionKey) return encryptionKey;

  const rawKey = process.env["PROVIDER_SECRET_ENCRYPTION_KEY"];
  if (!rawKey || rawKey.trim().length === 0) {
    logger.warn(
      "PROVIDER_SECRET_ENCRYPTION_KEY is not set — credential encryption will use a degraded fallback. " +
        "This is NOT safe for production."
    );
    encryptionKey = crypto.randomBytes(KEY_LENGTH);
  } else {
    encryptionKey = deriveKey(rawKey);
  }

  return encryptionKey;
}

export function encrypt(plaintext: string): string {
  const key = getKey();
  const iv = crypto.randomBytes(IV_LENGTH);
  const cipher = crypto.createCipheriv(ALGORITHM, key, iv);
  const encrypted = Buffer.concat([cipher.update(plaintext, "utf8"), cipher.final()]);
  // Format: base64(iv):base64(ciphertext)
  return `${iv.toString("base64")}:${encrypted.toString("base64")}`;
}

export function decrypt(ciphertext: string): string {
  const key = getKey();
  const parts = ciphertext.split(":");
  if (parts.length !== 2) throw new Error("Invalid ciphertext format");
  const iv = Buffer.from(parts[0]!, "base64");
  const encrypted = Buffer.from(parts[1]!, "base64");
  const decipher = crypto.createDecipheriv(ALGORITHM, key, iv);
  const decrypted = Buffer.concat([decipher.update(encrypted), decipher.final()]);
  return decrypted.toString("utf8");
}

export function maskSecret(value: string | null | undefined): string {
  if (!value || value.trim().length === 0) return "***not_configured***";
  return "***configured***";
}
