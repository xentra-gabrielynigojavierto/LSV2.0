'use client';

import { useState, useTransition } from 'react';
import type { IntegrityCheckpoint } from '@/types/control-center';

interface Props {
  checkpoints: IntegrityCheckpoint[];
}

/**
 * IntegrityPanel — displays existing integrity checkpoints and allows
 * generating a new one on demand.
 */
export function IntegrityPanel({ checkpoints }: Props) {
  const [, startTransition]     = useTransition();
  const [generating, setGenerating] = useState(false);
  const [newCheckpoint, setNewCheckpoint] = useState<IntegrityCheckpoint | null>(null);
  const [genError, setGenError] = useState<string | null>(null);
  const [checkpointType, setCheckpointType] = useState('manual-audit');
  const [fromDate, setFromDate] = useState('');
  const [toDate,   setToDate]   = useState('');

  async function handleGenerate(e: React.FormEvent) {
    e.preventDefault();
    setGenerating(true);
    setGenError(null);
    setNewCheckpoint(null);

    startTransition(() => {});

    try {
      const body = {
        checkpointType: checkpointType || undefined,
        fromRecordedAtUtc: fromDate ? `${fromDate}T00:00:00Z` : undefined,
        toRecordedAtUtc:   toDate   ? `${toDate}T23:59:59Z`   : undefined,
      };

      const res = await fetch('/api/synqaudit/integrity/generate', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(body),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({ message: `HTTP ${res.status}` })) as { message?: string };
        setGenError(err.message ?? `HTTP ${res.status}`);
      } else {
        const data = await res.json() as IntegrityCheckpoint;
        setNewCheckpoint(data);
      }
    } catch (err) {
      setGenError(err instanceof Error ? err.message : 'Request failed');
    } finally {
      setGenerating(false);
    }
  }

  const inputCls = 'w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white';
  const labelCls = 'block text-xs font-medium text-gray-600 mb-1';

  const allCheckpoints = newCheckpoint
    ? [newCheckpoint, ...checkpoints]
    : checkpoints;

  return (
    <div className="space-y-6">

      {/* Generate form */}
      <form onSubmit={handleGenerate} className="rounded-lg border border-gray-200 bg-white p-5 space-y-4">
        <h3 className="text-sm font-semibold text-gray-700">Generate New Checkpoint</h3>

        <div className="flex flex-wrap gap-4">
          <div className="w-48">
            <label className={labelCls}>Checkpoint Type</label>
            <input type="text" value={checkpointType} onChange={e => setCheckpointType(e.target.value)} className={inputCls} placeholder="manual-audit" />
          </div>
          <div className="w-40">
            <label className={labelCls}>From Date</label>
            <input type="date" value={fromDate} onChange={e => setFromDate(e.target.value)} className={inputCls} />
          </div>
          <div className="w-40">
            <label className={labelCls}>To Date</label>
            <input type="date" value={toDate} onChange={e => setToDate(e.target.value)} className={inputCls} />
          </div>
          <div className="flex items-end">
            <button
              type="submit"
              disabled={generating}
              className="h-9 px-4 text-sm font-semibold text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-md transition-colors"
            >
              {generating ? 'Generating…' : 'Generate'}
            </button>
          </div>
        </div>

        {genError && (
          <p className="text-sm text-red-600">{genError}</p>
        )}
      </form>

      {/* Checkpoint list */}
      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
          <h3 className="text-sm font-semibold text-gray-700">
            Integrity Checkpoints
            {allCheckpoints.length > 0 && (
              <span className="ml-2 text-xs font-normal text-gray-400">({allCheckpoints.length})</span>
            )}
          </h3>
        </div>

        {allCheckpoints.length === 0 ? (
          <div className="px-6 py-12 text-center">
            <p className="text-sm text-gray-400">No checkpoints yet. Generate one above.</p>
          </div>
        ) : (
          <div className="divide-y divide-gray-100">
            {allCheckpoints.map((cp, idx) => (
              <CheckpointRow key={cp.checkpointId || idx} checkpoint={cp} isNew={idx === 0 && !!newCheckpoint} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function CheckpointRow({ checkpoint: cp, isNew }: { checkpoint: IntegrityCheckpoint; isNew: boolean }) {
  const [expanded, setExpanded] = useState(isNew);

  const validityColor = cp.isValid === true
    ? 'text-green-700'
    : cp.isValid === false
      ? 'text-red-700'
      : 'text-gray-400';

  return (
    <div className={`px-4 py-3 ${isNew ? 'bg-green-50' : ''}`}>
      <button
        className="w-full flex items-center justify-between text-left"
        onClick={() => setExpanded(e => !e)}
      >
        <div className="flex items-center gap-3">
          {isNew && (
            <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold bg-green-100 text-green-700 border border-green-300">
              NEW
            </span>
          )}
          <div>
            <span className="text-xs font-semibold text-gray-700">{cp.checkpointType || 'checkpoint'}</span>
            <span className="ml-2 text-[10px] text-gray-400 font-mono">{cp.checkpointId?.slice(0, 16)}…</span>
          </div>
        </div>
        <div className="flex items-center gap-3 shrink-0">
          {cp.isValid !== undefined && (
            <span className={`text-[10px] font-semibold uppercase ${validityColor}`}>
              {cp.isValid ? '✓ valid' : '✗ invalid'}
            </span>
          )}
          <span className="text-[10px] text-gray-400">{cp.recordCount?.toLocaleString()} records</span>
          <span className="text-xs text-gray-400">{expanded ? '▲' : '▼'}</span>
        </div>
      </button>

      {expanded && (
        <div className="mt-3 space-y-1.5 pl-2">
          <Field label="Checkpoint ID">{cp.checkpointId}</Field>
          <Field label="Hash (HMAC-SHA256)">
            <span className="font-mono text-[10px] break-all">{cp.aggregateHash}</span>
          </Field>
          <Field label="Records">{cp.recordCount?.toLocaleString()}</Field>
          <Field label="From">{cp.fromRecordedAtUtc}</Field>
          <Field label="To">{cp.toRecordedAtUtc}</Field>
          <Field label="Created">{cp.createdAtUtc}</Field>
        </div>
      )}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex gap-2 text-[11px]">
      <span className="text-gray-400 w-32 shrink-0">{label}</span>
      <span className="text-gray-700">{children}</span>
    </div>
  );
}
