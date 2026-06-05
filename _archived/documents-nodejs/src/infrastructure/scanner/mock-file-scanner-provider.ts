import type { FileScannerProvider, ScanResult } from '@/domain/interfaces/file-scanner-provider';
import { logger } from '@/shared/logger';

/**
 * MockFileScannerProvider — deterministic scanner for dev/test.
 *
 * Trigger rules (checked in order):
 *  1. Buffer contains the EICAR test string  → INFECTED  (threats: ['EICAR-Test-File'])
 *  2. Buffer starts with bytes [0xFF, 0x4D, 0x41, 0x4C]  → INFECTED  (custom marker)
 *  3. Filename contains ".fail."  → FAILED  (simulates scanner error)
 *  4. Everything else             → CLEAN
 *
 * The EICAR test string is the industry-standard harmless file used to verify AV scanners.
 * It is safe to include in source code and test data.
 */

const EICAR_TEST_STRING = 'X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*';
const MOCK_INFECTED_MAGIC = Buffer.from([0xff, 0x4d, 0x41, 0x4c]);

export class MockFileScannerProvider implements FileScannerProvider {
  async scan(buffer: Buffer, filename: string): Promise<ScanResult> {
    const start = Date.now();

    // Simulate scanner latency
    await new Promise((r) => setTimeout(r, 5));

    const scanDurationMs = Date.now() - start;
    const scannedAt      = new Date();

    // ── Trigger: EICAR test string ──────────────────────────────────────────
    if (buffer.includes(Buffer.from(EICAR_TEST_STRING, 'utf8'))) {
      logger.warn({ filename, trigger: 'eicar' }, '[mock-scanner] INFECTED — EICAR test string');
      return {
        status:        'INFECTED',
        threats:       ['EICAR-Standard-AV-Test-File'],
        scanDurationMs,
        scannedAt,
        engineVersion: 'mock-1.0.0',
      };
    }

    // ── Trigger: magic bytes ────────────────────────────────────────────────
    if (
      buffer.length >= 4 &&
      buffer[0] === MOCK_INFECTED_MAGIC[0] &&
      buffer[1] === MOCK_INFECTED_MAGIC[1] &&
      buffer[2] === MOCK_INFECTED_MAGIC[2] &&
      buffer[3] === MOCK_INFECTED_MAGIC[3]
    ) {
      logger.warn({ filename, trigger: 'magic-bytes' }, '[mock-scanner] INFECTED — magic bytes');
      return {
        status:        'INFECTED',
        threats:       ['Mock.Malware.TestSignature'],
        scanDurationMs,
        scannedAt,
        engineVersion: 'mock-1.0.0',
      };
    }

    // ── Trigger: ".fail." in filename ───────────────────────────────────────
    if (filename.includes('.fail.')) {
      logger.warn({ filename, trigger: 'fail-marker' }, '[mock-scanner] FAILED — filename trigger');
      return {
        status:        'FAILED',
        scanDurationMs,
        scannedAt,
        engineVersion: 'mock-1.0.0',
      };
    }

    // ── Default: CLEAN ──────────────────────────────────────────────────────
    logger.debug({ filename }, '[mock-scanner] CLEAN');
    return {
      status:        'CLEAN',
      scanDurationMs,
      scannedAt,
      engineVersion: 'mock-1.0.0',
    };
  }

  providerName(): string {
    return 'mock';
  }
}
