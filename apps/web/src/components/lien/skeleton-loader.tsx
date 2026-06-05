'use client';

export function SkeletonRow({ cols = 5 }: { cols?: number }) {
  return (
    <tr className="animate-pulse">
      {Array.from({ length: cols }).map((_, i) => (
        <td key={i} className="px-4 py-3">
          <div className="h-4 bg-gray-100 rounded w-3/4" />
        </td>
      ))}
    </tr>
  );
}

export function SkeletonTable({ rows = 5, cols = 5 }: { rows?: number; cols?: number }) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <table className="min-w-full">
        <thead>
          <tr className="bg-gray-50">
            {Array.from({ length: cols }).map((_, i) => (
              <th key={i} className="px-4 py-3"><div className="h-3 bg-gray-200 rounded w-20 animate-pulse" /></th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {Array.from({ length: rows }).map((_, i) => <SkeletonRow key={i} cols={cols} />)}
        </tbody>
      </table>
    </div>
  );
}

export function SkeletonCard() {
  return (
    <div className="bg-white border border-gray-200 rounded-xl p-5 animate-pulse space-y-3">
      <div className="h-3 bg-gray-100 rounded w-1/3" />
      <div className="h-6 bg-gray-100 rounded w-1/2" />
      <div className="h-3 bg-gray-100 rounded w-2/3" />
    </div>
  );
}

export function SkeletonDetail() {
  return (
    <div className="space-y-5 animate-pulse">
      <div className="bg-white border border-gray-200 rounded-xl px-6 py-5 space-y-3">
        <div className="h-3 bg-gray-100 rounded w-20" />
        <div className="h-6 bg-gray-200 rounded w-48" />
        <div className="flex gap-4"><div className="h-3 bg-gray-100 rounded w-32" /><div className="h-3 bg-gray-100 rounded w-32" /></div>
      </div>
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <div className="bg-white border border-gray-200 rounded-xl p-5 space-y-3">
          {Array.from({ length: 4 }).map((_, i) => (<div key={i} className="h-4 bg-gray-100 rounded w-full" />))}
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-5 space-y-3">
          {Array.from({ length: 4 }).map((_, i) => (<div key={i} className="h-4 bg-gray-100 rounded w-full" />))}
        </div>
      </div>
    </div>
  );
}
