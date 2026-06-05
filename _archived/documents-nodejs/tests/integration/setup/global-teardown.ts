/**
 * Jest globalTeardown — runs ONCE after all test files have completed.
 * Removes all documents/versions created during integration test runs
 * (identified by product_id = 'int-test').
 *
 * Audit rows cannot be deleted (immutable trigger) — they accumulate harmlessly.
 */
export default async function globalTeardown(): Promise<void> {
  // Same env-var pattern as globalSetup — separate Node process
  const dbUrl =
    process.env['DATABASE_URL'] ??
    'postgresql://postgres:password@helium/heliumdb?sslmode=disable';

  // eslint-disable-next-line @typescript-eslint/no-var-requires
  const { Pool } = require('pg') as typeof import('pg');
  const pool = new Pool({ connectionString: dbUrl, max: 2 });

  try {
    // Hard-delete versions first (FK constraint), then documents
    await pool.query(`
      DELETE FROM document_versions
      WHERE document_id IN (
        SELECT id FROM documents WHERE product_id = 'int-test'
      )
    `);

    const { rowCount } = await pool.query(
      "DELETE FROM documents WHERE product_id = 'int-test'",
    );

    console.log(`[global-teardown] Removed ${rowCount ?? 0} integration test documents`);
  } catch (err) {
    console.error('[global-teardown] Cleanup error (non-fatal):', (err as Error).message);
  } finally {
    await pool.end();
  }
}
