import { Pool, PoolClient, QueryResultRow } from 'pg';
import { config }  from '@/shared/config';
import { logger }  from '@/shared/logger';
import { DatabaseError } from '@/shared/errors';

let _pool: Pool | null = null;

export function getPool(): Pool {
  if (!_pool) {
    _pool = new Pool({
      connectionString: config.DATABASE_URL,
      max:              10,
      idleTimeoutMillis: 30_000,
      connectionTimeoutMillis: 5_000,
    });

    _pool.on('error', (err) => {
      logger.error({ err: err.message }, 'Unexpected PostgreSQL pool error');
    });
  }
  return _pool;
}

/** Execute a query, wrapping PG errors in DatabaseError */
export async function query<T extends QueryResultRow = Record<string, unknown>>(
  sql: string,
  params?: unknown[],
): Promise<T[]> {
  const pool = getPool();
  try {
    const result = await pool.query<T>(sql, params);
    return result.rows;
  } catch (err) {
    logger.error({ err: (err as Error).message, sql: sql.slice(0, 80) }, 'DB query error');
    throw new DatabaseError(`Database query failed: ${(err as Error).message}`);
  }
}

/** Execute a query and return a single row or null */
export async function queryOne<T extends QueryResultRow = Record<string, unknown>>(
  sql: string,
  params?: unknown[],
): Promise<T | null> {
  const rows = await query<T>(sql, params);
  return rows[0] ?? null;
}

/** Run multiple queries in a transaction */
export async function withTransaction<T>(
  fn: (client: PoolClient) => Promise<T>,
): Promise<T> {
  const client = await getPool().connect();
  try {
    await client.query('BEGIN');
    const result = await fn(client);
    await client.query('COMMIT');
    return result;
  } catch (err) {
    await client.query('ROLLBACK');
    throw err;
  } finally {
    client.release();
  }
}

/** Health check */
export async function pingDatabase(): Promise<boolean> {
  try {
    await query('SELECT 1');
    return true;
  } catch {
    return false;
  }
}
