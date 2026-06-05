-- LS-FLOW-MERGE-P3 — Legacy "default" tenant cleanup
--
-- Phase 2 introduced strict tenant resolution. Any pre-Phase-2 rows written
-- with the placeholder TenantId "default" are now unreachable through the
-- API (the global tenant query filter excludes them).
--
-- This script:
--   1. Reports legacy rows per table (SELECT only, no writes).
--   2. Wraps an OPTIONAL deletion block — KEEP COMMENTED OUT and review the
--      counts from step 1 before uncommenting.
--
-- Idempotent: safe to re-run. Assumes the Flow MySQL schema (flow_db).
--
-- Manual usage (NOT auto-executed by the platform):
--   mysql -h <host> -u <user> -p flow_db < cleanup-default-tenant.sql

-- ---------------------------------------------------------------------------
-- 1. REPORT — legacy "default"-tenant row counts per Flow table.
-- ---------------------------------------------------------------------------
SELECT 'flow_definitions' AS table_name, COUNT(*) AS legacy_rows
FROM flow_definitions WHERE TenantId = 'default'
UNION ALL
SELECT 'flow_task_items', COUNT(*) FROM flow_task_items WHERE TenantId = 'default'
UNION ALL
SELECT 'flow_workflow_stages', COUNT(*) FROM flow_workflow_stages WHERE TenantId = 'default'
UNION ALL
SELECT 'flow_workflow_transitions', COUNT(*) FROM flow_workflow_transitions WHERE TenantId = 'default'
UNION ALL
SELECT 'flow_automation_hooks', COUNT(*) FROM flow_automation_hooks WHERE TenantId = 'default'
UNION ALL
SELECT 'flow_automation_actions', COUNT(*) FROM flow_automation_actions WHERE TenantId = 'default'
UNION ALL
SELECT 'flow_automation_execution_logs', COUNT(*) FROM flow_automation_execution_logs WHERE TenantId = 'default'
UNION ALL
SELECT 'flow_notifications', COUNT(*) FROM flow_notifications WHERE TenantId = 'default'
UNION ALL
SELECT 'flow_product_workflow_mappings', COUNT(*) FROM flow_product_workflow_mappings WHERE TenantId = 'default';

-- ---------------------------------------------------------------------------
-- 2. DELETE — uncomment after reviewing the counts above.
--    Order respects FK constraints (children first).
-- ---------------------------------------------------------------------------
-- START TRANSACTION;
-- DELETE FROM flow_automation_execution_logs WHERE TenantId = 'default';
-- DELETE FROM flow_automation_actions        WHERE TenantId = 'default';
-- DELETE FROM flow_automation_hooks          WHERE TenantId = 'default';
-- DELETE FROM flow_notifications             WHERE TenantId = 'default';
-- DELETE FROM flow_product_workflow_mappings WHERE TenantId = 'default';
-- DELETE FROM flow_task_items                WHERE TenantId = 'default';
-- DELETE FROM flow_workflow_transitions      WHERE TenantId = 'default';
-- DELETE FROM flow_workflow_stages           WHERE TenantId = 'default';
-- DELETE FROM flow_definitions               WHERE TenantId = 'default';
-- COMMIT;
