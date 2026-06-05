import type { FileScannerProvider, ScanResult } from '@/domain/interfaces/file-scanner-provider';
import { logger } from '@/shared/logger';
import { config } from '@/shared/config';

/**
 * ClamAvFileScannerProvider — ClamAV daemon (clamd) integration scaffold.
 *
 * ClamAV communicates via TCP socket using the INSTREAM command.
 * Protocol:
 *   1. Send: `zINSTREAM\0`
 *   2. Send file in chunks: [4-byte big-endian length][chunk bytes]
 *   3. Send terminator: [4 bytes of 0x00]
 *   4. Read response: "stream: OK\n" (clean) or "stream: <threat> FOUND\n" (infected)
 *
 * To activate:
 *   1. Install ClamAV: `apt-get install clamav-daemon` or Docker: `clamav/clamav`
 *   2. Install clamdjs: `npm install clamdjs`
 *   3. Set FILE_SCANNER_PROVIDER=clamav, CLAMAV_HOST, CLAMAV_PORT
 *   4. Uncomment the implementation below
 *
 * Docker quick-start:
 *   docker run -d --name clamav -p 3310:3310 clamav/clamav:latest
 */
export class ClamAvFileScannerProvider implements FileScannerProvider {
  // private readonly host: string;
  // private readonly port: number;

  constructor() {
    logger.info(
      { host: config.CLAMAV_HOST, port: config.CLAMAV_PORT },
      'ClamAV provider initialising (scaffold)',
    );

    // Uncomment when implementing:
    // this.host = config.CLAMAV_HOST;
    // this.port = config.CLAMAV_PORT;
  }

  async scan(_buffer: Buffer, _filename: string): Promise<ScanResult> {
    const start = Date.now();

    // ── Uncomment to implement via raw TCP socket: ─────────────────────────
    //
    // const net = await import('net');
    // const CHUNK_SIZE = 4096;
    //
    // return new Promise((resolve) => {
    //   const socket = net.createConnection(this.port, this.host);
    //   const chunks: Buffer[] = [];
    //
    //   socket.on('connect', () => {
    //     // Send INSTREAM command
    //     socket.write(Buffer.from('zINSTREAM\0'));
    //
    //     // Stream file in chunks
    //     let offset = 0;
    //     while (offset < _buffer.length) {
    //       const chunk  = _buffer.slice(offset, offset + CHUNK_SIZE);
    //       const lenBuf = Buffer.alloc(4);
    //       lenBuf.writeUInt32BE(chunk.length, 0);
    //       socket.write(lenBuf);
    //       socket.write(chunk);
    //       offset += CHUNK_SIZE;
    //     }
    //     // Terminator
    //     socket.write(Buffer.alloc(4));
    //   });
    //
    //   socket.on('data', (d) => chunks.push(d));
    //
    //   socket.on('end', () => {
    //     const response = Buffer.concat(chunks).toString().trim();
    //     const scanDurationMs = Date.now() - start;
    //     if (response.includes('FOUND')) {
    //       const threat = response.replace('stream: ', '').replace(' FOUND', '');
    //       resolve({ status: 'INFECTED', threats: [threat], scanDurationMs, scannedAt: new Date() });
    //     } else {
    //       resolve({ status: 'CLEAN', scanDurationMs, scannedAt: new Date() });
    //     }
    //   });
    //
    //   socket.on('error', (err) => {
    //     logger.error({ err: err.message }, 'ClamAV socket error');
    //     resolve({ status: 'FAILED', scanDurationMs: Date.now() - start, scannedAt: new Date() });
    //   });
    //
    //   socket.setTimeout(30_000, () => {
    //     socket.destroy();
    //     resolve({ status: 'FAILED', scanDurationMs: Date.now() - start, scannedAt: new Date() });
    //   });
    // });

    logger.warn({ filename: _filename }, 'ClamAvFileScannerProvider is a scaffold — returning FAILED');
    return {
      status:        'FAILED',
      scanDurationMs: Date.now() - start,
      scannedAt:     new Date(),
      engineVersion: 'clamav-scaffold',
    };
  }

  providerName(): string {
    return 'clamav';
  }
}

/**
 * NullFileScannerProvider — used when FILE_SCANNER_PROVIDER=none.
 * Returns SKIPPED immediately. Files will be accessible without scanning.
 */
export class NullFileScannerProvider implements FileScannerProvider {
  async scan(_buffer: Buffer, _filename: string): Promise<ScanResult> {
    return {
      status:        'SKIPPED',
      scanDurationMs: 0,
      scannedAt:     new Date(),
    };
  }

  providerName(): string {
    return 'none';
  }
}
