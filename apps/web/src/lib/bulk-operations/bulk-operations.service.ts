import type { BulkOperationResult, BulkItemResult } from './bulk-operations.types';

export async function executeBulk(
  ids: string[],
  handler: (id: string) => Promise<void>,
): Promise<BulkOperationResult> {
  const results: BulkItemResult[] = [];

  for (const id of ids) {
    try {
      await handler(id);
      results.push({ id, success: true });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error';
      results.push({ id, success: false, error: message });
    }
  }

  const succeededCount = results.filter((r) => r.success).length;
  const failedCount = results.filter((r) => !r.success).length;

  return {
    totalCount: ids.length,
    succeededCount,
    failedCount,
    skippedCount: 0,
    results,
  };
}
