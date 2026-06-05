-- Documents Service .NET — MySQL DDL
-- Run this as an alternative to EF Core migrations in environments
-- where dotnet-ef tooling is unavailable.

-- ── docs_documents ──────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS docs_documents (
    id                CHAR(36)     PRIMARY KEY,
    tenant_id         CHAR(36)     NOT NULL,
    product_id        VARCHAR(100) NOT NULL,
    reference_id      VARCHAR(500) NOT NULL,
    reference_type    VARCHAR(100) NOT NULL,
    document_type_id  CHAR(36)     NOT NULL,
    title             VARCHAR(500) NOT NULL,
    description       VARCHAR(2000),
    status            VARCHAR(20)  NOT NULL DEFAULT 'DRAFT',
    mime_type         VARCHAR(200) NOT NULL,
    file_size_bytes   BIGINT       NOT NULL,
    storage_key       TEXT         NOT NULL,
    storage_bucket    VARCHAR(200) NOT NULL,
    checksum          VARCHAR(200),
    current_version_id CHAR(36),
    version_count     INT          NOT NULL DEFAULT 0,
    scan_status       VARCHAR(20)  NOT NULL DEFAULT 'PENDING',
    scan_completed_at DATETIME(6),
    scan_duration_ms  INT,
    scan_threats      JSON         NOT NULL,
    scan_engine_version VARCHAR(100),
    is_published_as_logo TINYINT(1) NOT NULL DEFAULT 0,
    is_deleted        TINYINT(1)   NOT NULL DEFAULT 0,
    deleted_at        DATETIME(6),
    deleted_by        CHAR(36),
    retain_until      DATETIME(6),
    legal_hold_at     DATETIME(6),
    created_at        DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    created_by        CHAR(36)     NOT NULL,
    updated_at        DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    updated_by        CHAR(36)     NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX idx_documents_tenant    ON docs_documents (tenant_id);
CREATE INDEX idx_documents_product   ON docs_documents (tenant_id, product_id);
CREATE INDEX idx_documents_reference ON docs_documents (tenant_id, reference_id(191));

-- ── docs_document_versions ──────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS docs_document_versions (
    id                CHAR(36)     PRIMARY KEY,
    document_id       CHAR(36)     NOT NULL,
    tenant_id         CHAR(36)     NOT NULL,
    version_number    INT          NOT NULL,
    mime_type         VARCHAR(200) NOT NULL,
    file_size_bytes   BIGINT       NOT NULL,
    storage_key       TEXT         NOT NULL,
    storage_bucket    VARCHAR(200) NOT NULL,
    checksum          VARCHAR(200),
    scan_status       VARCHAR(20)  NOT NULL DEFAULT 'PENDING',
    scan_completed_at DATETIME(6),
    scan_duration_ms  INT,
    scan_threats      JSON         NOT NULL,
    scan_engine_version VARCHAR(100),
    label             VARCHAR(200),
    is_deleted        TINYINT(1)   NOT NULL DEFAULT 0,
    deleted_at        DATETIME(6),
    deleted_by        CHAR(36),
    uploaded_at       DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    uploaded_by       CHAR(36)     NOT NULL,

    CONSTRAINT fk_versions_document FOREIGN KEY (document_id) REFERENCES docs_documents(id),
    CONSTRAINT uq_version_number UNIQUE (document_id, version_number)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX idx_versions_document ON docs_document_versions (document_id, tenant_id);

-- ── docs_document_audits ────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS docs_document_audits (
    id             CHAR(36)     PRIMARY KEY,
    tenant_id      CHAR(36)     NOT NULL,
    document_id    CHAR(36),
    event          VARCHAR(100) NOT NULL,
    actor_id       CHAR(36),
    actor_email    VARCHAR(500),
    outcome        VARCHAR(20)  NOT NULL DEFAULT 'SUCCESS',
    ip_address     VARCHAR(50),
    user_agent     VARCHAR(1000),
    correlation_id VARCHAR(100),
    detail         JSON,
    occurred_at    DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    CONSTRAINT fk_audits_document FOREIGN KEY (document_id) REFERENCES docs_documents(id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX idx_audits_document ON docs_document_audits (document_id, tenant_id);
CREATE INDEX idx_audits_tenant   ON docs_document_audits (tenant_id, occurred_at);

-- ── docs_file_blobs (fallback storage) ──────────────────────────────────────
CREATE TABLE IF NOT EXISTS docs_file_blobs (
    storage_key    VARCHAR(500)  PRIMARY KEY,
    content        LONGBLOB     NOT NULL,
    mime_type      VARCHAR(200) NOT NULL DEFAULT 'application/octet-stream',
    size_bytes     BIGINT       NOT NULL DEFAULT 0,
    created_at_utc DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
