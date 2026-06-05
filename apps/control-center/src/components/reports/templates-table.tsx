import Link from 'next/link';
import type { ReportTemplate } from '@/types/control-center';

interface TemplatesTableProps {
  templates: ReportTemplate[];
}

export function TemplatesTable({ templates }: TemplatesTableProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <div>
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Report Templates
          </h2>
          <p className="text-[11px] text-gray-400 mt-0.5">
            Managed report definitions and their current versions
          </p>
        </div>
        <div className="flex items-center gap-3">
          <span className="text-xs text-gray-400 tabular-nums">
            {templates.length} template{templates.length !== 1 ? 's' : ''}
          </span>
          <Link
            href="/reports/templates/new"
            className="text-xs font-medium text-blue-600 hover:text-blue-800 inline-flex items-center gap-1"
          >
            <i className="ri-add-line" />
            Create
          </Link>
        </div>
      </div>

      {templates.length === 0 ? (
        <div className="px-5 py-8 text-center">
          <p className="text-sm text-gray-400">No templates registered.</p>
          <p className="text-xs text-gray-400 mt-1">
            Templates created via the Reports API will appear here.
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-left">
                <th className="px-5 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">Code</th>
                <th className="px-5 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">Name</th>
                <th className="px-5 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">Product</th>
                <th className="px-5 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">Org Type</th>
                <th className="px-5 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide text-center">Version</th>
                <th className="px-5 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide text-center">Status</th>
                <th className="px-5 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide text-right">Updated</th>
                <th className="px-5 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {templates.map(t => (
                <tr key={t.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-5 py-3 font-mono text-xs text-gray-700">{t.code}</td>
                  <td className="px-5 py-3 font-medium text-gray-900">{t.name}</td>
                  <td className="px-5 py-3 text-gray-600">{t.productCode}</td>
                  <td className="px-5 py-3 text-gray-600">{t.organizationType}</td>
                  <td className="px-5 py-3 text-center tabular-nums text-gray-700">v{t.currentVersion}</td>
                  <td className="px-5 py-3 text-center">
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold ${
                      t.isActive
                        ? 'bg-green-100 text-green-700'
                        : 'bg-gray-100 text-gray-500'
                    }`}>
                      {t.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="px-5 py-3 text-right text-xs text-gray-400 tabular-nums">
                    {formatDate(t.updatedAtUtc)}
                  </td>
                  <td className="px-5 py-3 text-right">
                    <Link
                      href={`/reports/templates/${t.id}`}
                      className="text-xs font-medium text-blue-600 hover:text-blue-800"
                    >
                      Edit
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      timeZone: 'UTC',
    });
  } catch {
    return iso;
  }
}
