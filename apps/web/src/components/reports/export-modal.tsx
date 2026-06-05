'use client';

import { useState } from 'react';
import type { ExportFormat } from '@/lib/reports/reports.types';

interface ExportModalProps {
  open: boolean;
  onClose: () => void;
  onExport: (format: ExportFormat) => Promise<void>;
  reportName?: string;
}

const FORMAT_OPTIONS: { value: ExportFormat; label: string; icon: string; desc: string }[] = [
  { value: 'CSV', label: 'CSV', icon: 'ri-file-text-line', desc: 'Comma-separated values' },
  { value: 'XLSX', label: 'Excel', icon: 'ri-file-excel-2-line', desc: 'Microsoft Excel workbook' },
  { value: 'PDF', label: 'PDF', icon: 'ri-file-pdf-2-line', desc: 'Portable Document Format' },
];

export function ExportModal({ open, onClose, onExport, reportName }: ExportModalProps) {
  const [format, setFormat] = useState<ExportFormat>('CSV');
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!open) return null;

  async function handleExport() {
    setExporting(true);
    setError(null);
    try {
      await onExport(format);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Export failed');
    } finally {
      setExporting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40" onClick={onClose} />
      <div className="relative bg-white rounded-xl shadow-xl w-full max-w-md mx-4">
        <div className="px-6 py-4 border-b border-gray-200">
          <div className="flex items-center justify-between">
            <h3 className="text-base font-semibold text-gray-900">Export Report</h3>
            <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
              <i className="ri-close-line text-xl" />
            </button>
          </div>
          {reportName && (
            <p className="text-sm text-gray-500 mt-1">{reportName}</p>
          )}
        </div>

        <div className="px-6 py-4 space-y-3">
          <label className="text-sm font-medium text-gray-700">Select format</label>
          <div className="space-y-2">
            {FORMAT_OPTIONS.map((opt) => (
              <label
                key={opt.value}
                className={`flex items-center gap-3 p-3 rounded-lg border cursor-pointer transition-colors ${
                  format === opt.value
                    ? 'border-primary bg-primary/5'
                    : 'border-gray-200 hover:border-gray-300'
                }`}
              >
                <input
                  type="radio"
                  name="format"
                  value={opt.value}
                  checked={format === opt.value}
                  onChange={() => setFormat(opt.value)}
                  className="sr-only"
                />
                <i className={`${opt.icon} text-lg ${format === opt.value ? 'text-primary' : 'text-gray-400'}`} />
                <div className="flex-1">
                  <p className={`text-sm font-medium ${format === opt.value ? 'text-primary' : 'text-gray-700'}`}>
                    {opt.label}
                  </p>
                  <p className="text-xs text-gray-500">{opt.desc}</p>
                </div>
                {format === opt.value && (
                  <i className="ri-check-line text-primary" />
                )}
              </label>
            ))}
          </div>

          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2">
              <p className="text-xs text-red-700">{error}</p>
            </div>
          )}
        </div>

        <div className="px-6 py-4 border-t border-gray-200 flex items-center justify-end gap-3">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            onClick={handleExport}
            disabled={exporting}
            className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-50"
          >
            {exporting ? (
              <span className="inline-flex items-center gap-2">
                <i className="ri-loader-4-line animate-spin" />
                Exporting...
              </span>
            ) : (
              <span className="inline-flex items-center gap-2">
                <i className="ri-download-2-line" />
                Export
              </span>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
