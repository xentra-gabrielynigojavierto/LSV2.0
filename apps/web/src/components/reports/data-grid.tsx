'use client';

import { useState, useMemo } from 'react';
import type { ReportColumnDto, ReportRowDto } from '@/lib/reports/reports.types';

interface DataGridProps {
  columns: ReportColumnDto[];
  rows: ReportRowDto[];
  maxHeight?: string;
}

type SortDir = 'asc' | 'desc' | null;

export function DataGrid({ columns, rows, maxHeight = '500px' }: DataGridProps) {
  const [sortCol, setSortCol] = useState<string | null>(null);
  const [sortDir, setSortDir] = useState<SortDir>(null);

  const sorted = useMemo(() => {
    if (!sortCol || !sortDir) return rows;
    return [...rows].sort((a, b) => {
      const va = a.values[sortCol];
      const vb = b.values[sortCol];
      if (va == null && vb == null) return 0;
      if (va == null) return 1;
      if (vb == null) return -1;
      const cmp = String(va).localeCompare(String(vb), undefined, { numeric: true });
      return sortDir === 'desc' ? -cmp : cmp;
    });
  }, [rows, sortCol, sortDir]);

  function toggleSort(colName: string) {
    if (sortCol !== colName) {
      setSortCol(colName);
      setSortDir('asc');
    } else if (sortDir === 'asc') {
      setSortDir('desc');
    } else {
      setSortCol(null);
      setSortDir(null);
    }
  }

  const orderedCols = useMemo(
    () => [...columns].sort((a, b) => a.order - b.order),
    [columns],
  );

  if (columns.length === 0) {
    return (
      <div className="bg-gray-50 border border-gray-200 rounded-lg px-6 py-10 text-center">
        <p className="text-sm text-gray-500">No data to display.</p>
      </div>
    );
  }

  return (
    <div className="border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-auto" style={{ maxHeight }}>
        <table className="w-full text-sm">
          <thead className="bg-gray-50 sticky top-0 z-10">
            <tr>
              <th className="px-3 py-2.5 text-left text-xs font-semibold text-gray-500 w-10">#</th>
              {orderedCols.map((col) => (
                <th
                  key={col.name}
                  onClick={() => toggleSort(col.name)}
                  className="px-3 py-2.5 text-left text-xs font-semibold text-gray-500 cursor-pointer hover:text-gray-800 select-none whitespace-nowrap"
                >
                  <span className="inline-flex items-center gap-1">
                    {col.label}
                    {sortCol === col.name && (
                      <i className={`ri-arrow-${sortDir === 'asc' ? 'up' : 'down'}-s-line text-primary`} />
                    )}
                  </span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {sorted.length === 0 ? (
              <tr>
                <td colSpan={orderedCols.length + 1} className="px-3 py-8 text-center text-sm text-gray-400">
                  No rows returned.
                </td>
              </tr>
            ) : (
              sorted.map((row) => (
                <tr key={row.rowNumber} className="hover:bg-gray-50/50">
                  <td className="px-3 py-2 text-xs text-gray-400">{row.rowNumber}</td>
                  {orderedCols.map((col) => (
                    <td key={col.name} className="px-3 py-2 text-gray-700 whitespace-nowrap">
                      {row.formattedValues?.[col.name] ?? formatCell(row.values[col.name], col.dataType)}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      <div className="bg-gray-50 border-t border-gray-200 px-3 py-2 text-xs text-gray-500 flex items-center justify-between">
        <span>{rows.length} row{rows.length !== 1 ? 's' : ''}</span>
        <span>{orderedCols.length} column{orderedCols.length !== 1 ? 's' : ''}</span>
      </div>
    </div>
  );
}

function formatCell(value: unknown, dataType: string): string {
  if (value == null) return '—';
  if (dataType === 'DateTime' || dataType === 'Date') {
    try {
      return new Date(String(value)).toLocaleDateString();
    } catch {
      return String(value);
    }
  }
  if (dataType === 'Currency' || dataType === 'Decimal') {
    const num = Number(value);
    if (!isNaN(num)) {
      return dataType === 'Currency'
        ? new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(num)
        : num.toLocaleString();
    }
  }
  if (dataType === 'Boolean') {
    return value === true || value === 'true' ? 'Yes' : 'No';
  }
  return String(value);
}
