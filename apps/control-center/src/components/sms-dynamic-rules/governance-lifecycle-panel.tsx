'use client';

import { useState } from 'react';
import type {
  RuleVersion,
  RulePackVersion,
  RuleEffectivenessRow,
  FalsePositiveCandidateRow,
  PackEffectivenessRow,
  GovernanceImportResult,
  ImportBundle,
} from '@/lib/sms-governance-lifecycle-api';
import {
  getRuleVersionHistory,
  getRulePackVersionHistory,
  rollbackRule,
  rollbackRulePack,
  validateGovernanceImport,
  importGovernanceRules,
  exportGovernanceRules,
  getRuleEffectiveness,
  getFalsePositiveCandidates,
  getPackEffectiveness,
} from '@/lib/sms-governance-lifecycle-api';

// ─── Shared helpers ───────────────────────────────────────────────────────────

function ChangeTypeBadge({ type }: { type: string }) {
  const cls: Record<string, string> = {
    created:  'bg-green-100 text-green-700 border-green-200',
    updated:  'bg-blue-100 text-blue-600 border-blue-200',
    disabled: 'bg-slate-100 text-slate-500 border-slate-200',
    rollback: 'bg-orange-100 text-orange-700 border-orange-200',
    imported: 'bg-purple-100 text-purple-700 border-purple-200',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border ${cls[type] ?? 'bg-slate-100 text-slate-600 border-slate-200'}`}>
      {type}
    </span>
  );
}

function fmtDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: 'short',
    timeStyle: 'short',
  });
}

function fmtPct(v: number) {
  return (v * 100).toFixed(1) + '%';
}

// ─── Version History Panel ────────────────────────────────────────────────────

function VersionHistoryPanel() {
  const [mode, setMode]           = useState<'rule' | 'pack'>('rule');
  const [entityId, setEntityId]   = useState('');
  const [loading, setLoading]     = useState(false);
  const [error, setError]         = useState<string | null>(null);
  const [ruleVersions, setRuleVersions]   = useState<RuleVersion[]>([]);
  const [packVersions, setPackVersions]   = useState<RulePackVersion[]>([]);

  // Rollback state
  const [targetVersion, setTargetVersion] = useState<number | null>(null);
  const [rollbackReason, setRollbackReason] = useState('');
  const [rolling, setRolling] = useState(false);
  const [rollbackMsg, setRollbackMsg] = useState<string | null>(null);

  const load = async () => {
    if (!entityId.trim()) { setError('Enter an ID'); return; }
    setLoading(true); setError(null);
    setRuleVersions([]); setPackVersions([]);
    setRollbackMsg(null); setTargetVersion(null);
    try {
      if (mode === 'rule') {
        const r = await getRuleVersionHistory(entityId.trim());
        setRuleVersions(r.versions);
      } else {
        const r = await getRulePackVersionHistory(entityId.trim());
        setPackVersions(r.versions);
      }
    } catch (e: any) {
      setError(e.message ?? 'Failed to load version history');
    } finally {
      setLoading(false);
    }
  };

  const doRollback = async () => {
    if (!targetVersion) return;
    if (!confirm(`Roll back to version ${targetVersion}? A new version will be created preserving this rollback.`)) return;
    setRolling(true); setRollbackMsg(null);
    try {
      const result = mode === 'rule'
        ? await rollbackRule(entityId.trim(), targetVersion, undefined, rollbackReason || undefined)
        : await rollbackRulePack(entityId.trim(), targetVersion, undefined, rollbackReason || undefined);
      setRollbackMsg(result.message);
      await load();
    } catch (e: any) {
      setError(e.message ?? 'Rollback failed');
    } finally {
      setRolling(false);
    }
  };

  const versions = mode === 'rule' ? ruleVersions : packVersions;

  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-500">
        Browse immutable version snapshots for any governance rule or rule pack. You can roll back to any previous version — rollback creates a new version, history is never deleted.
      </p>

      <div className="flex gap-3 items-end flex-wrap">
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Entity Type</label>
          <div className="flex rounded-md overflow-hidden border border-slate-300">
            {(['rule', 'pack'] as const).map(m => (
              <button
                key={m}
                onClick={() => { setMode(m); setRuleVersions([]); setPackVersions([]); }}
                className={`px-4 py-1.5 text-sm font-medium transition-colors ${mode === m ? 'bg-indigo-600 text-white' : 'bg-white text-slate-600 hover:bg-slate-50'}`}
              >
                {m === 'rule' ? 'Rule' : 'Rule Pack'}
              </button>
            ))}
          </div>
        </div>
        <div className="flex-1 min-w-48">
          <label className="block text-xs font-medium text-slate-600 mb-1">{mode === 'rule' ? 'Rule ID' : 'Rule Pack ID'}</label>
          <input
            value={entityId}
            onChange={e => setEntityId(e.target.value)}
            placeholder={`Paste ${mode === 'rule' ? 'rule' : 'pack'} UUID…`}
            className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400"
          />
        </div>
        <button
          onClick={load}
          disabled={loading}
          className="px-4 py-1.5 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700 disabled:opacity-50"
        >
          {loading ? 'Loading…' : 'Load History'}
        </button>
      </div>

      {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded px-3 py-2 text-sm">{error}</div>}
      {rollbackMsg && <div className="bg-green-50 border border-green-200 text-green-700 rounded px-3 py-2 text-sm">{rollbackMsg}</div>}

      {versions.length > 0 && (
        <>
          <div className="overflow-x-auto rounded-lg border border-slate-200">
            <table className="min-w-full text-sm">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-200">
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600">Version</th>
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600">Change Type</th>
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600">Reason</th>
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600">Created</th>
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600">By</th>
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600">Rollback</th>
                </tr>
              </thead>
              <tbody>
                {versions.map((v, i) => (
                  <tr key={v.id} className={`border-b border-slate-100 hover:bg-slate-50 ${i === 0 ? 'bg-indigo-50/40' : ''}`}>
                    <td className="px-4 py-2 font-mono text-slate-700">
                      v{v.versionNumber}
                      {i === 0 && <span className="ml-2 text-xs text-indigo-600 font-sans">(current)</span>}
                    </td>
                    <td className="px-4 py-2"><ChangeTypeBadge type={v.changeType} /></td>
                    <td className="px-4 py-2 text-slate-500 max-w-48 truncate">{v.changeReason ?? '—'}</td>
                    <td className="px-4 py-2 text-slate-500">{fmtDate(v.createdAt)}</td>
                    <td className="px-4 py-2 text-slate-500 max-w-32 truncate">{v.createdBy ?? '—'}</td>
                    <td className="px-4 py-2">
                      {i > 0 && (
                        <button
                          onClick={() => setTargetVersion(v.versionNumber)}
                          className={`text-xs font-medium rounded px-2 py-0.5 transition-colors ${
                            targetVersion === v.versionNumber
                              ? 'bg-orange-100 text-orange-700 ring-1 ring-orange-300'
                              : 'text-orange-600 hover:bg-orange-50'
                          }`}
                        >
                          Select v{v.versionNumber}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {targetVersion && (
            <div className="border border-orange-200 bg-orange-50 rounded-lg p-4 space-y-3">
              <p className="text-sm font-medium text-orange-800">
                Roll back to version {targetVersion}? A new snapshot (ChangeType=rollback) will be created.
              </p>
              <div>
                <label className="block text-xs font-medium text-orange-700 mb-1">Reason (optional)</label>
                <input
                  value={rollbackReason}
                  onChange={e => setRollbackReason(e.target.value)}
                  placeholder="Reason for rollback…"
                  className="w-full rounded-md border border-orange-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400"
                />
              </div>
              <div className="flex gap-2">
                <button
                  onClick={doRollback}
                  disabled={rolling}
                  className="px-4 py-1.5 bg-orange-600 text-white text-sm font-medium rounded-md hover:bg-orange-700 disabled:opacity-50"
                >
                  {rolling ? 'Rolling back…' : `Confirm Rollback to v${targetVersion}`}
                </button>
                <button
                  onClick={() => setTargetVersion(null)}
                  className="px-4 py-1.5 bg-white text-slate-600 text-sm border border-slate-300 rounded-md hover:bg-slate-50"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {!loading && versions.length === 0 && entityId && (
        <p className="text-sm text-slate-400 italic">No version history found for this ID.</p>
      )}
    </div>
  );
}

// ─── Import / Export Panel ────────────────────────────────────────────────────

const IMPORT_EXAMPLE = JSON.stringify(
  {
    bundles: [
      {
        rulePack: {
          name: "Example Pack",
          status: "active",
          inheritanceMode: "merge",
          priority: 100,
          enabled: true,
        },
        rules: [
          {
            name: "Block URGENT keyword",
            ruleType: "prohibited_phrase",
            severity: "block",
            pattern: "URGENT",
            enabled: true,
            priority: 10,
          },
        ],
      },
    ],
    dryRun: false,
  },
  null, 2,
);

function ImportExportPanel() {
  const [jsonText, setJsonText]         = useState('');
  const [dryRun, setDryRun]             = useState(true);
  const [validating, setValidating]     = useState(false);
  const [importing, setImporting]       = useState(false);
  const [result, setResult]             = useState<GovernanceImportResult | null>(null);
  const [exportLoading, setExportLoading] = useState(false);
  const [exportData, setExportData]     = useState<string | null>(null);
  const [error, setError]               = useState<string | null>(null);

  const parsePayload = (): { bundles: ImportBundle[] } | null => {
    try { return JSON.parse(jsonText); }
    catch { setError('Invalid JSON — check your payload'); return null; }
  };

  const handleValidate = async () => {
    const payload = parsePayload();
    if (!payload) return;
    setValidating(true); setError(null); setResult(null);
    try {
      const r = await validateGovernanceImport({ bundles: payload.bundles, dryRun: true });
      setResult(r);
    } catch (e: any) {
      setError(e.message ?? 'Validation failed');
    } finally {
      setValidating(false);
    }
  };

  const handleImport = async () => {
    const payload = parsePayload();
    if (!payload) return;
    if (!dryRun && !confirm('This will persist the imported rule packs and rules to the database. Continue?')) return;
    setImporting(true); setError(null); setResult(null);
    try {
      const r = await importGovernanceRules({ bundles: payload.bundles, dryRun });
      setResult(r);
    } catch (e: any) {
      setError(e.message ?? 'Import failed');
    } finally {
      setImporting(false);
    }
  };

  const handleExport = async () => {
    setExportLoading(true); setError(null); setExportData(null);
    try {
      const data = await exportGovernanceRules({ includeProfiles: false });
      setExportData(JSON.stringify(data, null, 2));
    } catch (e: any) {
      setError(e.message ?? 'Export failed');
    } finally {
      setExportLoading(false);
    }
  };

  const downloadExport = () => {
    if (!exportData) return;
    const blob = new Blob([exportData], { type: 'application/json' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href = url; a.download = `governance-export-${new Date().toISOString().slice(0,10)}.json`;
    a.click(); URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-6">

      {/* Import section */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-slate-700">Bulk Import</h3>
          <button
            onClick={() => setJsonText(IMPORT_EXAMPLE)}
            className="text-xs text-indigo-600 hover:underline"
          >
            Load example
          </button>
        </div>
        <p className="text-xs text-slate-500">
          Paste a JSON payload containing rule pack bundles. Import is transactional — all bundles succeed or the entire operation is rolled back. Use dry-run to validate before committing.
        </p>

        <textarea
          value={jsonText}
          onChange={e => setJsonText(e.target.value)}
          rows={12}
          placeholder='Paste JSON payload here (see "Load example" for schema)…'
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-indigo-400 resize-y"
          spellCheck={false}
        />

        <div className="flex items-center gap-4 flex-wrap">
          <label className="flex items-center gap-2 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={dryRun}
              onChange={e => setDryRun(e.target.checked)}
              className="rounded"
            />
            <span className="text-sm text-slate-600">Dry-run (validate only, no writes)</span>
          </label>

          <button
            onClick={handleValidate}
            disabled={validating || !jsonText.trim()}
            className="px-4 py-1.5 bg-slate-600 text-white text-sm font-medium rounded-md hover:bg-slate-700 disabled:opacity-50"
          >
            {validating ? 'Validating…' : 'Validate'}
          </button>
          <button
            onClick={handleImport}
            disabled={importing || !jsonText.trim()}
            className={`px-4 py-1.5 text-white text-sm font-medium rounded-md disabled:opacity-50 transition-colors ${
              dryRun ? 'bg-indigo-500 hover:bg-indigo-600' : 'bg-indigo-700 hover:bg-indigo-800'
            }`}
          >
            {importing ? 'Importing…' : dryRun ? 'Import (dry-run)' : 'Import (commit)'}
          </button>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded px-3 py-2 text-sm">{error}</div>
        )}

        {result && (
          <div className={`rounded-lg border p-4 text-sm space-y-2 ${
            result.isValid ? 'bg-green-50 border-green-200' : 'bg-red-50 border-red-200'
          }`}>
            <div className="flex items-center gap-2 font-medium">
              <span>{result.isValid ? '✓' : '✗'}</span>
              <span className={result.isValid ? 'text-green-800' : 'text-red-800'}>
                {result.isValid ? (result.persisted ? 'Import successful' : 'Validation passed — no data written') : 'Validation failed'}
              </span>
            </div>
            {result.isValid && result.persisted && (
              <p className="text-green-700">{result.bundlesImported} pack(s) and {result.rulesImported} rule(s) imported.</p>
            )}
            {result.errors && result.errors.length > 0 && (
              <ul className="space-y-1 mt-2">
                {result.errors.map((e, i) => (
                  <li key={i} className="text-red-700 text-xs">
                    <span className="font-mono text-red-500 mr-1">[bundle {e.bundleIndex}{e.ruleIndex >= 0 ? `, rule ${e.ruleIndex}` : ''}: {e.field}]</span>
                    {e.message}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </div>

      {/* Export section */}
      <div className="border-t border-slate-100 pt-5 space-y-3">
        <h3 className="text-sm font-semibold text-slate-700">Export</h3>
        <p className="text-xs text-slate-500">
          Export all governance rules and packs as JSON. The exported payload can be re-imported into any LegalSynq environment. No message content, phone numbers, or tenant secrets are included.
        </p>
        <button
          onClick={handleExport}
          disabled={exportLoading}
          className="px-4 py-1.5 bg-slate-700 text-white text-sm font-medium rounded-md hover:bg-slate-800 disabled:opacity-50"
        >
          {exportLoading ? 'Exporting…' : 'Export All Rules'}
        </button>

        {exportData && (
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <span className="text-xs text-green-700 font-medium">Export ready</span>
              <button onClick={downloadExport} className="text-xs text-indigo-600 hover:underline">
                Download JSON
              </button>
            </div>
            <textarea
              readOnly
              value={exportData}
              rows={8}
              className="w-full rounded-md border border-slate-200 px-3 py-2 text-xs font-mono bg-slate-50 resize-y"
            />
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Effectiveness Analytics Panel ───────────────────────────────────────────

function EffectivenessPanel() {
  const [loading, setLoading]           = useState(false);
  const [error, setError]               = useState<string | null>(null);
  const [effectRows, setEffectRows]     = useState<RuleEffectivenessRow[]>([]);
  const [fpCandidates, setFpCandidates] = useState<FalsePositiveCandidateRow[]>([]);
  const [packRows, setPackRows]         = useState<PackEffectivenessRow[]>([]);
  const [subTab, setSubTab]             = useState<'rules' | 'packs' | 'fp'>('rules');

  const load = async () => {
    setLoading(true); setError(null);
    try {
      const [eff, fp, packs] = await Promise.allSettled([
        getRuleEffectiveness({ pageSize: 100 }),
        getFalsePositiveCandidates(),
        getPackEffectiveness({ pageSize: 50 }),
      ]);
      if (eff.status === 'fulfilled')   setEffectRows(eff.value.rows);
      if (fp.status === 'fulfilled')    setFpCandidates(fp.value.candidates);
      if (packs.status === 'fulfilled') setPackRows(packs.value.rows);
      if (eff.status === 'rejected' && fp.status === 'rejected' && packs.status === 'rejected')
        setError('All analytics requests failed — service may be unavailable');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-500">
          Aggregate rule match metrics — no message content or phone numbers. Data is recorded as daily buckets and reflects both live and simulation traffic.
        </p>
        <button
          onClick={load}
          disabled={loading}
          className="px-4 py-1.5 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700 disabled:opacity-50 whitespace-nowrap"
        >
          {loading ? 'Loading…' : 'Load Analytics'}
        </button>
      </div>

      {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded px-3 py-2 text-sm">{error}</div>}

      {(effectRows.length > 0 || fpCandidates.length > 0 || packRows.length > 0) && (
        <>
          <div className="flex gap-1 border-b border-slate-200">
            {([
              { id: 'rules', label: 'Rule Effectiveness', count: effectRows.length },
              { id: 'packs', label: 'Pack Effectiveness', count: packRows.length },
              { id: 'fp',    label: `False Positive Candidates`, count: fpCandidates.length },
            ] as const).map(t => (
              <button
                key={t.id}
                onClick={() => setSubTab(t.id)}
                className={`px-3 py-2 text-xs font-medium border-b-2 transition-colors ${
                  subTab === t.id ? 'border-indigo-600 text-indigo-600' : 'border-transparent text-slate-500 hover:text-slate-700'
                }`}
              >
                {t.label}
                {t.count > 0 && <span className="ml-1.5 bg-slate-100 rounded-full px-1.5">{t.count}</span>}
              </button>
            ))}
          </div>

          {/* Rule Effectiveness Table */}
          {subTab === 'rules' && (
            <div className="overflow-x-auto rounded-lg border border-slate-200">
              <table className="min-w-full text-xs">
                <thead>
                  <tr className="bg-slate-50 border-b border-slate-200 text-slate-600">
                    <th className="text-left px-3 py-2.5 font-medium">Rule</th>
                    <th className="text-left px-3 py-2.5 font-medium">Type</th>
                    <th className="text-left px-3 py-2.5 font-medium">Severity</th>
                    <th className="text-right px-3 py-2.5 font-medium">Matches</th>
                    <th className="text-right px-3 py-2.5 font-medium">Block</th>
                    <th className="text-right px-3 py-2.5 font-medium">Warn</th>
                    <th className="text-right px-3 py-2.5 font-medium">Review</th>
                    <th className="text-right px-3 py-2.5 font-medium">Block%</th>
                    <th className="text-right px-3 py-2.5 font-medium">Live</th>
                    <th className="text-right px-3 py-2.5 font-medium">Sim</th>
                    <th className="text-left px-3 py-2.5 font-medium">Last match</th>
                  </tr>
                </thead>
                <tbody>
                  {effectRows.map((r, i) => (
                    <tr key={i} className="border-b border-slate-100 hover:bg-slate-50">
                      <td className="px-3 py-2">
                        <span className="font-medium text-slate-700 truncate block max-w-40">{r.ruleName ?? r.ruleId ?? '—'}</span>
                      </td>
                      <td className="px-3 py-2 text-slate-500">{r.ruleType ?? '—'}</td>
                      <td className="px-3 py-2">
                        {r.severity && (
                          <span className={`px-1.5 py-0.5 rounded text-xs font-medium ${
                            r.severity === 'block' ? 'bg-red-100 text-red-700' :
                            r.severity === 'warn'  ? 'bg-yellow-100 text-yellow-700' :
                            r.severity === 'review_required' ? 'bg-orange-100 text-orange-700' :
                            'bg-green-100 text-green-700'
                          }`}>{r.severity}</span>
                        )}
                      </td>
                      <td className="px-3 py-2 text-right font-semibold text-slate-700">{r.totalMatches.toLocaleString()}</td>
                      <td className="px-3 py-2 text-right text-red-600">{r.blockCount.toLocaleString()}</td>
                      <td className="px-3 py-2 text-right text-yellow-600">{r.warnCount.toLocaleString()}</td>
                      <td className="px-3 py-2 text-right text-orange-600">{r.reviewCount.toLocaleString()}</td>
                      <td className="px-3 py-2 text-right">
                        <span className={`font-medium ${r.blockRate > 0.5 ? 'text-red-600' : r.blockRate > 0.2 ? 'text-orange-500' : 'text-slate-500'}`}>
                          {fmtPct(r.blockRate)}
                        </span>
                      </td>
                      <td className="px-3 py-2 text-right text-slate-500">{r.liveCount.toLocaleString()}</td>
                      <td className="px-3 py-2 text-right text-slate-400">{r.simulationCount.toLocaleString()}</td>
                      <td className="px-3 py-2 text-slate-400">{r.lastMatchedAt ? fmtDate(r.lastMatchedAt) : '—'}</td>
                    </tr>
                  ))}
                  {effectRows.length === 0 && (
                    <tr><td colSpan={11} className="px-3 py-6 text-center text-slate-400 italic">No match data recorded yet.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          {/* Pack Effectiveness Table */}
          {subTab === 'packs' && (
            <div className="overflow-x-auto rounded-lg border border-slate-200">
              <table className="min-w-full text-xs">
                <thead>
                  <tr className="bg-slate-50 border-b border-slate-200 text-slate-600">
                    <th className="text-left px-3 py-2.5 font-medium">Rule Pack</th>
                    <th className="text-right px-3 py-2.5 font-medium">Active Rules</th>
                    <th className="text-right px-3 py-2.5 font-medium">Matches</th>
                    <th className="text-right px-3 py-2.5 font-medium">Block</th>
                    <th className="text-right px-3 py-2.5 font-medium">Warn</th>
                    <th className="text-right px-3 py-2.5 font-medium">Block%</th>
                    <th className="text-left px-3 py-2.5 font-medium">Last match</th>
                  </tr>
                </thead>
                <tbody>
                  {packRows.map((r, i) => (
                    <tr key={i} className="border-b border-slate-100 hover:bg-slate-50">
                      <td className="px-3 py-2 font-medium text-slate-700 truncate max-w-48">{r.packName ?? r.rulePackId ?? '—'}</td>
                      <td className="px-3 py-2 text-right text-slate-500">{r.activeRules}</td>
                      <td className="px-3 py-2 text-right font-semibold text-slate-700">{r.totalMatches.toLocaleString()}</td>
                      <td className="px-3 py-2 text-right text-red-600">{r.blockCount.toLocaleString()}</td>
                      <td className="px-3 py-2 text-right text-yellow-600">{r.warnCount.toLocaleString()}</td>
                      <td className="px-3 py-2 text-right">
                        <span className={`font-medium ${r.blockRate > 0.5 ? 'text-red-600' : r.blockRate > 0.2 ? 'text-orange-500' : 'text-slate-500'}`}>
                          {fmtPct(r.blockRate)}
                        </span>
                      </td>
                      <td className="px-3 py-2 text-slate-400">{r.lastMatchedAt ? fmtDate(r.lastMatchedAt) : '—'}</td>
                    </tr>
                  ))}
                  {packRows.length === 0 && (
                    <tr><td colSpan={7} className="px-3 py-6 text-center text-slate-400 italic">No pack match data recorded yet.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          {/* False Positive Candidates */}
          {subTab === 'fp' && (
            <div className="space-y-3">
              <p className="text-xs text-slate-500">
                Rules that may be generating false positives, identified by heuristic analysis of warn rates and live-vs-simulation ratios. These are suggestions only — review and disable if appropriate.
              </p>
              {fpCandidates.length === 0
                ? <p className="text-sm text-slate-400 italic py-4">No false positive candidates detected. All rules appear to be matching expected traffic patterns.</p>
                : (
                  <div className="space-y-2">
                    {fpCandidates.map((c, i) => (
                      <div key={i} className="border border-orange-200 bg-orange-50 rounded-lg p-4">
                        <div className="flex items-start justify-between gap-4">
                          <div className="space-y-1">
                            <p className="text-sm font-medium text-orange-900">{c.ruleName ?? c.ruleId ?? 'Unknown rule'}</p>
                            <p className="text-xs text-orange-700">{c.heuristic}</p>
                          </div>
                          <div className="text-right shrink-0 space-y-1">
                            <p className="text-xs text-orange-500 font-medium">FP Score: {(c.fpScore * 100).toFixed(0)}%</p>
                            <p className="text-xs text-slate-500">{c.totalMatches} total matches</p>
                          </div>
                        </div>
                        <div className="mt-2 flex gap-4 text-xs text-slate-600">
                          <span>Warn: <strong>{c.warnCount}</strong></span>
                          <span>Live: <strong>{c.liveCount}</strong></span>
                          <span>Sim: <strong>{c.simulationCount}</strong></span>
                          {c.ruleType && <span>Type: <code className="bg-white/70 px-1 rounded">{c.ruleType}</code></span>}
                        </div>
                      </div>
                    ))}
                  </div>
                )
              }
            </div>
          )}
        </>
      )}

      {!loading && effectRows.length === 0 && packRows.length === 0 && fpCandidates.length === 0 && (
        <p className="text-sm text-slate-400 italic py-2">Click "Load Analytics" to fetch effectiveness data.</p>
      )}
    </div>
  );
}

// ─── Main Lifecycle Panel ─────────────────────────────────────────────────────

type LifecycleTab = 'versions' | 'import' | 'effectiveness';

const LIFECYCLE_TABS: { id: LifecycleTab; label: string }[] = [
  { id: 'versions',     label: 'Version History & Rollback' },
  { id: 'import',       label: 'Import / Export' },
  { id: 'effectiveness', label: 'Effectiveness Analytics' },
];

export function GovernanceLifecyclePanel() {
  const [tab, setTab] = useState<LifecycleTab>('versions');

  return (
    <div className="space-y-4">
      <div className="border-b border-slate-200 -mx-1">
        <nav className="flex gap-1">
          {LIFECYCLE_TABS.map(t => (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              className={`px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${
                tab === t.id
                  ? 'border-indigo-600 text-indigo-600'
                  : 'border-transparent text-slate-500 hover:text-slate-700'
              }`}
            >
              {t.label}
            </button>
          ))}
        </nav>
      </div>

      <div className="bg-white rounded-lg border border-slate-200 p-4">
        {tab === 'versions'      && <VersionHistoryPanel />}
        {tab === 'import'        && <ImportExportPanel />}
        {tab === 'effectiveness' && <EffectivenessPanel />}
      </div>
    </div>
  );
}
