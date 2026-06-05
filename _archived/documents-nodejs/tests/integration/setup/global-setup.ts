/**
 * Jest globalSetup — runs ONCE in a separate Node.js process before any test files.
 * Responsibilities:
 *  1. Run database migrations (idempotent)
 *  2. Seed shared base data (test document type)
 *  3. Ensure local storage directory exists
 *
 * IMPORTANT: env vars MUST be set before requiring any app module, because
 * config.ts parses process.env at module initialisation time.
 */

import fs from 'fs';

export default async function globalSetup(): Promise<void> {
  // ── 1. Inject env vars ─────────────────────────────────────────────────────
  process.env['NODE_ENV']                       = 'test';
  process.env['LOG_LEVEL']                      = 'error';
  process.env['DATABASE_URL']                   =
    process.env['DATABASE_URL'] ??
    'postgresql://postgres:password@helium/heliumdb?sslmode=disable';
  process.env['AUTH_PROVIDER']                  = 'jwt';
  process.env['JWT_SECRET']                     = 'integration-test-secret-do-not-use-in-prod';
  process.env['STORAGE_PROVIDER']               = 'local';
  process.env['LOCAL_STORAGE_PATH']             = '/tmp/docs-integration-test-storage';
  process.env['FILE_SCANNER_PROVIDER']          = 'none';
  process.env['REQUIRE_CLEAN_SCAN_FOR_ACCESS']  = 'true';
  process.env['MAX_FILE_SIZE_MB']               = '1';
  process.env['RATE_LIMIT_PROVIDER']            = 'memory';
  process.env['RATE_LIMIT_MAX_REQUESTS']        = '200';
  process.env['RATE_LIMIT_UPLOAD_MAX']          = '50';
  process.env['RATE_LIMIT_SIGNED_URL_MAX']      = '50';
  process.env['RATE_LIMIT_WINDOW_SECONDS']      = '3600';
  process.env['ACCESS_TOKEN_STORE']             = 'memory';
  process.env['ACCESS_TOKEN_TTL_SECONDS']       = '300';
  process.env['ACCESS_TOKEN_ONE_TIME_USE']      = 'true';
  process.env['DIRECT_PRESIGN_ENABLED']         = 'false';
  process.env['CORS_ORIGINS']                   = 'http://localhost:5000';
  delete process.env['REDIS_URL'];

  // ── 2. Ensure local storage directory exists ───────────────────────────────
  const storagePath = '/tmp/docs-integration-test-storage';
  if (!fs.existsSync(storagePath)) {
    fs.mkdirSync(storagePath, { recursive: true });
    console.log(`[global-setup] Created local storage dir: ${storagePath}`);
  }

  // ── 3. Run migrations ──────────────────────────────────────────────────────
  // Dynamic require AFTER env vars are set so config.ts parses correctly.
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  const { Pool } = require('pg') as typeof import('pg');
  const pool = new Pool({ connectionString: process.env['DATABASE_URL'] });

  try {
    // Ensure migrations tracking table exists
    await pool.query(`
      CREATE TABLE IF NOT EXISTS _docs_migrations (
        name       TEXT PRIMARY KEY,
        applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      )
    `);

    const migrations: Array<{ name: string; sql: string }> = [
      {
        name: '001_create_document_types',
        sql: `
          CREATE TABLE IF NOT EXISTS document_types (
            id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id    UUID,
            product_id   TEXT,
            code         TEXT NOT NULL,
            label        TEXT NOT NULL,
            is_active    BOOLEAN NOT NULL DEFAULT TRUE,
            created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            UNIQUE (tenant_id, code)
          )
        `,
      },
      {
        name: '002_create_documents',
        sql: `
          CREATE TABLE IF NOT EXISTS documents (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id           UUID NOT NULL,
            product_id          TEXT NOT NULL,
            reference_id        TEXT NOT NULL,
            reference_type      TEXT NOT NULL,
            document_type_id    UUID NOT NULL REFERENCES document_types(id),
            title               TEXT NOT NULL,
            description         TEXT,
            status              TEXT NOT NULL DEFAULT 'DRAFT',
            storage_key         TEXT NOT NULL,
            storage_bucket      TEXT NOT NULL,
            mime_type           TEXT NOT NULL,
            file_size_bytes     BIGINT NOT NULL DEFAULT 0,
            checksum            TEXT NOT NULL DEFAULT '',
            current_version_id  UUID,
            version_count       INT NOT NULL DEFAULT 0,
            scan_status         TEXT NOT NULL DEFAULT 'PENDING',
            scan_completed_at   TIMESTAMPTZ,
            scan_threats        TEXT[] NOT NULL DEFAULT '{}',
            is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
            deleted_at          TIMESTAMPTZ,
            deleted_by          UUID,
            retain_until        TIMESTAMPTZ,
            legal_hold_at       TIMESTAMPTZ,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            created_by          UUID NOT NULL,
            updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_by          UUID NOT NULL
          );
          CREATE INDEX IF NOT EXISTS idx_documents_tenant
            ON documents(tenant_id) WHERE is_deleted = FALSE;
          CREATE INDEX IF NOT EXISTS idx_documents_reference
            ON documents(tenant_id, reference_id, reference_type);
          CREATE INDEX IF NOT EXISTS idx_documents_product
            ON documents(tenant_id, product_id);
        `,
      },
      {
        name: '003_create_document_versions',
        sql: `
          CREATE TABLE IF NOT EXISTS document_versions (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            document_id     UUID NOT NULL REFERENCES documents(id),
            tenant_id       UUID NOT NULL,
            version_number  INT NOT NULL,
            storage_key     TEXT NOT NULL,
            storage_bucket  TEXT NOT NULL,
            mime_type       TEXT NOT NULL,
            file_size_bytes BIGINT NOT NULL DEFAULT 0,
            checksum        TEXT NOT NULL DEFAULT '',
            scan_status     TEXT NOT NULL DEFAULT 'PENDING',
            scan_completed_at TIMESTAMPTZ,
            scan_duration_ms  INT,
            scan_threats      TEXT[] NOT NULL DEFAULT '{}',
            scan_engine_version TEXT,
            uploaded_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            uploaded_by     UUID NOT NULL,
            label           TEXT,
            is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
            deleted_at      TIMESTAMPTZ,
            deleted_by      UUID,
            UNIQUE (document_id, version_number)
          );
          CREATE INDEX IF NOT EXISTS idx_versions_document
            ON document_versions(document_id);
        `,
      },
      {
        name: '004_create_document_audits',
        sql: `
          CREATE TABLE IF NOT EXISTS document_audits (
            id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id             UUID NOT NULL,
            document_id           UUID NOT NULL,
            document_version_id   UUID,
            event                 TEXT NOT NULL,
            actor_id              UUID NOT NULL,
            actor_roles           TEXT[] NOT NULL DEFAULT '{}',
            correlation_id        TEXT NOT NULL,
            ip_address            TEXT,
            user_agent            TEXT,
            outcome               TEXT NOT NULL DEFAULT 'SUCCESS',
            detail                JSONB NOT NULL DEFAULT '{}',
            occurred_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
          );
          CREATE OR REPLACE FUNCTION prevent_audit_mutation()
          RETURNS TRIGGER LANGUAGE plpgsql AS $$
          BEGIN
            RAISE EXCEPTION 'document_audits rows are immutable';
          END;
          $$;
          DROP TRIGGER IF EXISTS trg_audit_immutable ON document_audits;
          CREATE TRIGGER trg_audit_immutable
            BEFORE UPDATE OR DELETE ON document_audits
            FOR EACH ROW EXECUTE FUNCTION prevent_audit_mutation();
          CREATE INDEX IF NOT EXISTS idx_audits_tenant
            ON document_audits(tenant_id, occurred_at DESC);
          CREATE INDEX IF NOT EXISTS idx_audits_document
            ON document_audits(document_id, occurred_at DESC);
          CREATE INDEX IF NOT EXISTS idx_audits_actor
            ON document_audits(actor_id);
        `,
      },
    ];

    for (const m of migrations) {
      const existing = await pool.query(
        'SELECT name FROM _docs_migrations WHERE name = $1',
        [m.name],
      );
      if (existing.rowCount && existing.rowCount > 0) continue;

      await pool.query('BEGIN');
      try {
        await pool.query(m.sql);
        await pool.query('INSERT INTO _docs_migrations (name) VALUES ($1)', [m.name]);
        await pool.query('COMMIT');
        console.log(`[global-setup] Applied migration: ${m.name}`);
      } catch (err) {
        await pool.query('ROLLBACK');
        throw err;
      }
    }

    // ── 4. Seed shared test document type ─────────────────────────────────────
    await pool.query(`
      INSERT INTO document_types (id, code, label, is_active)
      VALUES ('10000000-0000-0000-0000-000000000001', 'INT_TEST_CONTRACT', 'Integration Test Contract', TRUE)
      ON CONFLICT (id) DO NOTHING
    `);

    console.log('[global-setup] Migrations complete — DB ready for integration tests');
  } finally {
    await pool.end();
  }
}
