-- =============================================================================
-- Notifications Microservice — Database Schema
-- Generated from Sequelize model definitions
-- All tables use snake_case columns; UUIDs as primary keys (unless noted)
-- =============================================================================

-- Enable UUID extension if not already active
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- =============================================================================
-- TENANTS
-- =============================================================================
CREATE TABLE tenants (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(255) NOT NULL,
    plan            VARCHAR(50)  NOT NULL DEFAULT 'free',
    status          VARCHAR(50)  NOT NULL DEFAULT 'active',
    metadata_json   TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- PROVIDER CONFIGS  (BYOP — Bring Your Own Provider, NOTIF-005)
-- =============================================================================
CREATE TABLE tenant_provider_configs (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               UUID        NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    channel                 VARCHAR(20) NOT NULL,   -- email | sms | push | in-app
    provider                VARCHAR(100) NOT NULL,
    ownership_mode          VARCHAR(20) NOT NULL DEFAULT 'platform',  -- platform | byop
    is_active               BOOLEAN     NOT NULL DEFAULT TRUE,
    priority                INTEGER     NOT NULL DEFAULT 0,
    credentials_encrypted   TEXT,                  -- AES-256-GCM encrypted JSON
    config_json             TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_tenant_provider_configs_tenant ON tenant_provider_configs(tenant_id);
CREATE INDEX idx_tenant_provider_configs_channel ON tenant_provider_configs(tenant_id, channel);

-- =============================================================================
-- CHANNEL PROVIDER SETTINGS
-- =============================================================================
CREATE TABLE tenant_channel_provider_settings (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID        NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    channel             VARCHAR(20) NOT NULL,
    preferred_provider  VARCHAR(100),
    fallback_enabled    BOOLEAN     NOT NULL DEFAULT FALSE,
    config_json         TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, channel)
);

-- =============================================================================
-- TEMPLATES  (NOTIF-004)
-- =============================================================================
CREATE TABLE templates (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    UUID         NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    key          VARCHAR(255) NOT NULL,
    name         VARCHAR(255) NOT NULL,
    channel      VARCHAR(20)  NOT NULL,
    description  TEXT,
    status       VARCHAR(50)  NOT NULL DEFAULT 'active',  -- active | archived
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, key)
);

CREATE INDEX idx_templates_tenant ON templates(tenant_id);

CREATE TABLE template_versions (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id  UUID         NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    tenant_id    UUID         NOT NULL,
    version      INTEGER      NOT NULL DEFAULT 1,
    subject      TEXT,
    body         TEXT         NOT NULL,
    text_body    TEXT,
    variables_json TEXT,
    is_active    BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (template_id, version)
);

CREATE INDEX idx_template_versions_template ON template_versions(template_id);

-- =============================================================================
-- NOTIFICATIONS  (core send record)
-- =============================================================================
CREATE TABLE notifications (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               UUID        NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    channel                 VARCHAR(20) NOT NULL,   -- email | sms | push | in-app
    status                  VARCHAR(30) NOT NULL DEFAULT 'accepted',
                            -- accepted | processing | sent | failed | blocked
    recipient_json          TEXT        NOT NULL,   -- {email} or {phoneNumber}
    message_json            TEXT        NOT NULL,   -- {subject,body,html} or {body}
    metadata_json           TEXT,
    idempotency_key         VARCHAR(255),
    provider_used           VARCHAR(100),
    failure_category        VARCHAR(100),
    last_error_message      TEXT,
    template_id             UUID REFERENCES templates(id) ON DELETE SET NULL,
    template_version_id     UUID REFERENCES template_versions(id) ON DELETE SET NULL,
    template_key            VARCHAR(255),
    rendered_subject        TEXT,
    rendered_body           TEXT,
    rendered_text           TEXT,
    provider_ownership_mode VARCHAR(50),
    provider_config_id      UUID REFERENCES tenant_provider_configs(id) ON DELETE SET NULL,
    platform_fallback_used  BOOLEAN     NOT NULL DEFAULT FALSE,
    -- NOTIF-007: contact enforcement traceability
    blocked_by_policy       BOOLEAN     NOT NULL DEFAULT FALSE,
    blocked_reason_code     VARCHAR(100),
    override_used           BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, idempotency_key)
        WHERE idempotency_key IS NOT NULL
);

CREATE INDEX idx_notifications_tenant ON notifications(tenant_id);
CREATE INDEX idx_notifications_status ON notifications(tenant_id, status);
CREATE INDEX idx_notifications_channel ON notifications(tenant_id, channel);
CREATE INDEX idx_notifications_created ON notifications(tenant_id, created_at DESC);

-- =============================================================================
-- NOTIFICATION ATTEMPTS  (per-provider send attempt)
-- =============================================================================
CREATE TABLE notification_attempts (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notification_id         UUID        NOT NULL REFERENCES notifications(id) ON DELETE CASCADE,
    tenant_id               UUID        NOT NULL,
    attempt_number          INTEGER     NOT NULL DEFAULT 1,
    provider                VARCHAR(100) NOT NULL,
    channel                 VARCHAR(20) NOT NULL,
    status                  VARCHAR(50) NOT NULL DEFAULT 'pending',
                            -- pending | in_flight | delivered | failed | bounced
    provider_message_id     VARCHAR(512),
    failure_category        VARCHAR(100),
    error_message           TEXT,
    provider_response_json  TEXT,
    ownership_mode          VARCHAR(20),
    provider_config_id      UUID,
    platform_fallback_used  BOOLEAN NOT NULL DEFAULT FALSE,
    sent_at                 TIMESTAMPTZ,
    delivered_at            TIMESTAMPTZ,
    failed_at               TIMESTAMPTZ,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_notification_attempts_notification ON notification_attempts(notification_id);
CREATE INDEX idx_notification_attempts_provider_msg ON notification_attempts(provider_message_id)
    WHERE provider_message_id IS NOT NULL;

-- =============================================================================
-- NOTIFICATION EVENTS  (webhook-ingested delivery events)
-- =============================================================================
CREATE TABLE notification_events (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                   UUID,
    notification_id             UUID REFERENCES notifications(id) ON DELETE SET NULL,
    notification_attempt_id     UUID REFERENCES notification_attempts(id) ON DELETE SET NULL,
    provider                    VARCHAR(100) NOT NULL,
    channel                     VARCHAR(20)  NOT NULL,
    raw_event_type              VARCHAR(255) NOT NULL,
    normalized_event_type       VARCHAR(100) NOT NULL,
                                -- delivered | bounced | complained | unsubscribed |
                                -- failed | opened | clicked | carrier_rejected | pending
    event_timestamp             TIMESTAMPTZ  NOT NULL,
    provider_message_id         VARCHAR(512),
    metadata_json               TEXT,
    dedup_key                   VARCHAR(512),
    created_at                  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (dedup_key) WHERE dedup_key IS NOT NULL
);

CREATE INDEX idx_notification_events_notification ON notification_events(notification_id)
    WHERE notification_id IS NOT NULL;
CREATE INDEX idx_notification_events_tenant ON notification_events(tenant_id)
    WHERE tenant_id IS NOT NULL;

-- =============================================================================
-- DELIVERY ISSUES  (persistent per-notification delivery problems)
-- =============================================================================
CREATE TABLE delivery_issues (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               UUID        NOT NULL,
    notification_id         UUID        NOT NULL REFERENCES notifications(id) ON DELETE CASCADE,
    notification_attempt_id UUID        REFERENCES notification_attempts(id) ON DELETE SET NULL,
    channel                 VARCHAR(20) NOT NULL,
    provider                VARCHAR(100) NOT NULL,
    issue_type              VARCHAR(100) NOT NULL,
    raw_event_type          VARCHAR(255),
    normalized_event_type   VARCHAR(100),
    recipient_contact       VARCHAR(512),
    error_code              VARCHAR(100),
    error_message           TEXT,
    resolved                BOOLEAN     NOT NULL DEFAULT FALSE,
    resolved_at             TIMESTAMPTZ,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_delivery_issues_notification ON delivery_issues(notification_id);
CREATE INDEX idx_delivery_issues_tenant ON delivery_issues(tenant_id);

-- =============================================================================
-- PROVIDER WEBHOOK LOGS  (raw inbound webhook payloads)
-- =============================================================================
CREATE TABLE provider_webhook_logs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    provider        VARCHAR(100) NOT NULL,
    channel         VARCHAR(20)  NOT NULL,
    raw_payload     TEXT         NOT NULL,
    headers_json    TEXT,
    verified        BOOLEAN      NOT NULL DEFAULT FALSE,
    processing_status VARCHAR(50) NOT NULL DEFAULT 'pending',
    error_message   TEXT,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- PROVIDER HEALTH  (circuit-breaker state per provider/channel)
-- =============================================================================
CREATE TABLE provider_health (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    provider            VARCHAR(100) NOT NULL,
    channel             VARCHAR(20)  NOT NULL,
    status              VARCHAR(50)  NOT NULL DEFAULT 'healthy',
                        -- healthy | degraded | unhealthy
    failure_count       INTEGER      NOT NULL DEFAULT 0,
    last_failure_at     TIMESTAMPTZ,
    last_success_at     TIMESTAMPTZ,
    circuit_open        BOOLEAN      NOT NULL DEFAULT FALSE,
    circuit_opened_at   TIMESTAMPTZ,
    metadata_json       TEXT,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (provider, channel)
);

-- =============================================================================
-- RECIPIENT CONTACT HEALTH  (per-contact deliverability state)
-- =============================================================================
CREATE TABLE recipient_contact_health (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID        NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    channel             VARCHAR(20) NOT NULL,
    contact_value       VARCHAR(512) NOT NULL,  -- normalized email or phone
    health_status       VARCHAR(50) NOT NULL DEFAULT 'valid',
                        -- valid | bounced | complained | unsubscribed |
                        -- suppressed | invalid | carrier_rejected | unreachable
    failure_count       INTEGER     NOT NULL DEFAULT 0,
    last_failure_category VARCHAR(100),
    last_event_type     VARCHAR(100),
    last_event_at       TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, channel, contact_value)
);

CREATE INDEX idx_contact_health_tenant ON recipient_contact_health(tenant_id);
CREATE INDEX idx_contact_health_status ON recipient_contact_health(tenant_id, health_status);

-- =============================================================================
-- CONTACT SUPPRESSIONS  (NOTIF-007)
-- =============================================================================
CREATE TABLE contact_suppressions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID         NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    channel             VARCHAR(50)  NOT NULL,
    contact_value       VARCHAR(512) NOT NULL,  -- normalized email or phone
    suppression_type    VARCHAR(50)  NOT NULL,
                        -- manual | bounce | unsubscribe | complaint |
                        -- invalid_contact | carrier_rejection | system_protection
    source              VARCHAR(50)  NOT NULL,
                        -- provider_webhook | manual_admin | system_rule | import
    status              VARCHAR(30)  NOT NULL DEFAULT 'active',
                        -- active | expired | lifted
    reason              TEXT         NOT NULL,
    notes               TEXT,
    created_by          VARCHAR(255),
    expires_at          TIMESTAMPTZ,
    lifted_at           TIMESTAMPTZ,
    lifted_by           VARCHAR(255),
    lift_reason         TEXT,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_contact_suppressions_tenant ON contact_suppressions(tenant_id);
CREATE INDEX idx_contact_suppressions_lookup ON contact_suppressions(tenant_id, channel, contact_value);
CREATE INDEX idx_contact_suppressions_active ON contact_suppressions(tenant_id, channel, contact_value, status)
    WHERE status = 'active';
CREATE INDEX idx_contact_suppressions_type ON contact_suppressions(tenant_id, suppression_type);

-- =============================================================================
-- TENANT CONTACT POLICIES  (NOTIF-007 — per-tenant blocking rules)
-- =============================================================================
CREATE TABLE tenant_contact_policies (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                   UUID        NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    channel                     VARCHAR(20),    -- NULL = global policy
    status                      VARCHAR(30) NOT NULL DEFAULT 'active',
    block_suppressed_contacts   BOOLEAN     NOT NULL DEFAULT TRUE,
    block_unsubscribed_contacts BOOLEAN     NOT NULL DEFAULT TRUE,
    block_complained_contacts   BOOLEAN     NOT NULL DEFAULT TRUE,
    block_bounced_contacts      BOOLEAN     NOT NULL DEFAULT FALSE,
    block_invalid_contacts      BOOLEAN     NOT NULL DEFAULT FALSE,
    block_carrier_rejected_contacts BOOLEAN NOT NULL DEFAULT FALSE,
    allow_manual_override       BOOLEAN     NOT NULL DEFAULT FALSE,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, channel) WHERE channel IS NOT NULL
);

CREATE INDEX idx_contact_policies_tenant ON tenant_contact_policies(tenant_id);

-- =============================================================================
-- BILLING PLANS  (NOTIF-006)
-- =============================================================================
CREATE TABLE tenant_billing_plans (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                   UUID        NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    plan_name                   VARCHAR(100) NOT NULL,
    status                      VARCHAR(50) NOT NULL DEFAULT 'active',
    monthly_notification_limit  INTEGER,
    monthly_template_render_limit INTEGER,
    api_rate_limit_per_minute   INTEGER,
    overage_allowed             BOOLEAN     NOT NULL DEFAULT FALSE,
    overage_rate_cents          INTEGER,
    billing_cycle_start         TIMESTAMPTZ,
    billing_cycle_end           TIMESTAMPTZ,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id)
);

CREATE TABLE tenant_billing_rates (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID        NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    usage_unit      VARCHAR(100) NOT NULL,
    rate_cents      INTEGER     NOT NULL DEFAULT 0,
    included_quota  INTEGER,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, usage_unit)
);

-- =============================================================================
-- USAGE METER EVENTS  (NOTIF-006 — fire-and-forget metering)
-- =============================================================================
CREATE TABLE usage_meter_events (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID        NOT NULL,
    usage_unit      VARCHAR(100) NOT NULL,
                    -- api_notification_request | template_render | provider_attempt |
                    -- failover_attempt | webhook_received | suppressed_notification_request_rejected
    channel         VARCHAR(20),
    notification_id UUID REFERENCES notifications(id) ON DELETE SET NULL,
    quantity        INTEGER     NOT NULL DEFAULT 1,
    metadata_json   TEXT,
    recorded_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_usage_meter_tenant ON usage_meter_events(tenant_id);
CREATE INDEX idx_usage_meter_unit ON usage_meter_events(tenant_id, usage_unit);
CREATE INDEX idx_usage_meter_recorded ON usage_meter_events(tenant_id, recorded_at DESC);

-- =============================================================================
-- RATE LIMIT POLICIES  (NOTIF-006)
-- =============================================================================
CREATE TABLE tenant_rate_limit_policies (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                   UUID        NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    channel                     VARCHAR(20),    -- NULL = global
    requests_per_minute         INTEGER,
    requests_per_hour           INTEGER,
    requests_per_day            INTEGER,
    burst_allowance             INTEGER,
    status                      VARCHAR(30) NOT NULL DEFAULT 'active',
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, channel) WHERE channel IS NOT NULL
);

-- =============================================================================
-- End of schema
-- =============================================================================
