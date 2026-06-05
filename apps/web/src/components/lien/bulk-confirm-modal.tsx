'use client';

import { Modal } from '@/components/lien/modal';

interface BulkConfirmModalProps {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  description: string;
  count: number;
  variant?: 'primary' | 'danger';
  loading?: boolean;
}

export function BulkConfirmModal({
  open,
  onClose,
  onConfirm,
  title,
  description,
  count,
  variant = 'primary',
  loading,
}: BulkConfirmModalProps) {
  const btnClass =
    variant === 'danger'
      ? 'bg-red-600 hover:bg-red-700 text-white'
      : 'bg-primary hover:bg-primary/90 text-white';

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      size="sm"
      footer={
        <>
          <button
            onClick={onClose}
            disabled={loading}
            className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            onClick={onConfirm}
            disabled={loading}
            className={`text-sm px-4 py-2 rounded-lg ${btnClass} disabled:opacity-50`}
          >
            {loading ? 'Processing...' : `Confirm (${count})`}
          </button>
        </>
      }
    >
      <div className="space-y-3">
        <div className="flex items-center gap-2">
          <span className="inline-flex items-center justify-center h-8 w-8 rounded-full bg-gray-100 text-sm font-semibold text-gray-700">
            {count}
          </span>
          <span className="text-sm text-gray-600">
            item{count !== 1 ? 's' : ''} will be affected
          </span>
        </div>
        <p className="text-sm text-gray-600">{description}</p>
      </div>
    </Modal>
  );
}
