/**
 * Database migration script — run via `npm run db:migrate`.
 * Uses raw SQL; no ORM dependency.
 */
import 'dotenv/config';
import { getPool } from './db';
import { logger }  from '@/shared/logger';

const MIGRATIONS = [
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
      );
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

      CREATE INDEX IF NOT EXISTS idx_documents_tenant       ON documents(tenant_id) WHERE is_deleted = FALSE;
      CREATE INDEX IF NOT EXISTS idx_documents_reference    ON documents(tenant_id, reference_id, reference_type);
      CREATE INDEX IF NOT EXISTS idx_documents_product      ON documents(tenant_id, product_id);
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

        uploaded_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        uploaded_by     UUID NOT NULL,
        label           TEXT,

        is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
        deleted_at      TIMESTAMPTZ,
        deleted_by      UUID,

        UNIQUE (document_id, version_number)
      );

      CREATE INDEX IF NOT EXISTS idx_versions_document ON document_versions(document_id);
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

      -- Audits are INSERT-only: enforce via trigger
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

      CREATE INDEX IF NOT EXISTS idx_audits_tenant   ON document_audits(tenant_id, occurred_at DESC);
      CREATE INDEX IF NOT EXISTS idx_audits_document ON document_audits(document_id, occurred_at DESC);
      CREATE INDEX IF NOT EXISTS idx_audits_actor    ON document_audits(actor_id);
    `,
  },
  {
    name: '005_create_migrations_table',
    sql: `
      CREATE TABLE IF NOT EXISTS _docs_migrations (
        name       TEXT PRIMARY KEY,
        applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );
    `,
  },
  {
    name: '006_add_scan_columns',
    sql: `
      -- Add scan lifecycle columns to documents (mirrors current version scan state)
      ALTER TABLE documents
        ADD COLUMN IF NOT EXISTS scan_status       TEXT NOT NULL DEFAULT 'PENDING',
        ADD COLUMN IF NOT EXISTS scan_completed_at TIMESTAMPTZ,
        ADD COLUMN IF NOT EXISTS scan_threats      TEXT[] NOT NULL DEFAULT '{}';

      -- Add extended scan detail columns to document_versions
      ALTER TABLE document_versions
        ADD COLUMN IF NOT EXISTS scan_duration_ms    INT,
        ADD COLUMN IF NOT EXISTS scan_threats        TEXT[] NOT NULL DEFAULT '{}',
        ADD COLUMN IF NOT EXISTS scan_engine_version TEXT;

      -- Index for fast querying of unscanned/infected documents
      CREATE INDEX IF NOT EXISTS idx_documents_scan_status ON documents(tenant_id, scan_status)
        WHERE is_deleted = FALSE;
    `,
  },
];

async function run() {
  const pool = getPool();
  const client = await pool.connect();
  try {
    // Ensure migrations table exists first
    await client.query(`
      CREATE TABLE IF NOT EXISTS _docs_migrations (
        name       TEXT PRIMARY KEY,
        applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );
    `);

    for (const migration of MIGRATIONS) {
      const existing = await client.query(
        'SELECT name FROM _docs_migrations WHERE name = $1',
        [migration.name],
      );
      if (existing.rowCount && existing.rowCount > 0) {
        logger.info({ migration: migration.name }, 'Migration already applied — skipping');
        continue;
      }

      logger.info({ migration: migration.name }, 'Applying migration');
      await client.query('BEGIN');
      try {
        await client.query(migration.sql);
        await client.query(
          'INSERT INTO _docs_migrations (name) VALUES ($1)',
          [migration.name],
        );
        await client.query('COMMIT');
        logger.info({ migration: migration.name }, 'Migration applied');
      } catch (err) {
        await client.query('ROLLBACK');
        throw err;
      }
    }

    logger.info('All migrations complete');
  } finally {
    client.release();
    await pool.end();
  }
}

run().catch((err) => {
  logger.error({ err: err.message }, 'Migration failed');
  process.exit(1);
});
