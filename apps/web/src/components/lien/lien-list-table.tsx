import Link from 'next/link';
import type { LienSummary } from '@/types/lien';
import { LIEN_TYPE_LABELS } from '@/types/lien';
import { LienStatusBadge } from './lien-status-badge';

interface LienListTableProps {
  liens:     LienSummary[];
  basePath?: string;   // e.g. '/lien/my-liens' or '/lien/portfolio'
  emptyText?: string;
}

function formatCurrency(amount?: number): string {
  if (amount == null) return '—';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
    .format(amount);
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

export function LienListTable({ liens, basePath = '/lien/my-liens', emptyText = 'No liens found.' }: LienListTableProps) {
  if (liens.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">{emptyText}</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Lien #</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Subject</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Original</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Offer price</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Jurisdiction</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {liens.map(lien => {
              const subject = lien.isConfidential
                ? <span className="text-xs italic text-gray-400">Confidential</span>
                : lien.subjectParty
                  ? <span>{lien.subjectParty.firstName} {lien.subjectParty.lastName}</span>
                  : <span className="text-gray-400">—</span>;

              return (
                <tr key={lien.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3">
                    <span className="text-xs font-mono text-gray-600">{lien.lienNumber}</span>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-700">
                    {LIEN_TYPE_LABELS[lien.lienType] ?? lien.lienType}
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-700">{subject}</td>
                  <td className="px-4 py-3 text-sm text-gray-700">{formatCurrency(lien.originalAmount)}</td>
                  <td className="px-4 py-3 text-sm text-gray-700">{formatCurrency(lien.offerPrice)}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{lien.jurisdiction ?? '—'}</td>
                  <td className="px-4 py-3">
                    <LienStatusBadge status={lien.status} />
                  </td>
                  <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                    {formatDate(lien.createdAtUtc)}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <Link
                      href={`${basePath}/${lien.id}`}
                      className="text-xs text-primary font-medium hover:underline whitespace-nowrap"
                    >
                      View →
                    </Link>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
