import type { ScanStatusValue } from '@/shared/constants';

/**
 * FileScannerProvider — pluggable malware / AV scanning abstraction.
 *
 * Implementations:
 *  - MockFileScannerProvider   (dev/test — trigger via buffer content)
 *  - ClamAvFileScannerProvider (production — scaffold)
 *
 * Designed for synchronous use now, async extension later:
 *  - Sync: scan buffer inline during upload; block on result
 *  - Async (future): upload to quarantine bucket → enqueue → worker calls scan()
 *    → update version scanStatus → move to clean bucket or quarantine
 */
export interface ScanResult {
  /** Outcome of the scan */
  status:       ScanStatusValue;
  /** Human-readable threat names detected (INFECTED only) */
  threats?:     string[];
  /** Wall-clock duration of the scan in milliseconds */
  scanDurationMs: number;
  /** When the scan completed */
  scannedAt:    Date;
  /** Provider-specific engine/signature version for audit trail */
  engineVersion?: string;
}

export interface FileScannerProvider {
  /**
   * Scan a file buffer for malware.
   * MUST NOT throw — return status=FAILED on internal errors.
   */
  scan(buffer: Buffer, filename: string): Promise<ScanResult>;

  /** Return the name of the active scanner for observability. */
  providerName(): string;
}
