export type BulkEntityType = 'case' | 'lien' | 'servicing' | 'contact' | 'document' | 'billOfSale';

export interface BulkActionConfig {
  key: string;
  label: string;
  icon: string;
  variant: 'primary' | 'danger';
  confirmTitle: string;
  confirmDescription: (count: number) => string;
}

export interface BulkItemResult {
  id: string;
  success: boolean;
  error?: string;
}

export interface BulkOperationResult {
  totalCount: number;
  succeededCount: number;
  failedCount: number;
  skippedCount: number;
  results: BulkItemResult[];
}

export type BulkExecutor = (ids: string[]) => Promise<BulkOperationResult>;
