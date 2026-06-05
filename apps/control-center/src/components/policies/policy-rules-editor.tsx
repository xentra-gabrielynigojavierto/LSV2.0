'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import type { PolicyRule } from '@/types/control-center';

const SUPPORTED_FIELDS = [
  'amount', 'organizationId', 'tenantId', 'region', 'caseId',
  'owner', 'time', 'ip', 'status', 'role', 'department',
];

const OPERATORS = [
  'Equals', 'NotEquals', 'GreaterThan', 'GreaterThanOrEqual',
  'LessThan', 'LessThanOrEqual', 'In', 'NotIn', 'Contains', 'StartsWith',
];

const CONDITION_TYPES = ['Attribute', 'Resource', 'Context'];
const LOGICAL_GROUPS = ['And', 'Or'];

interface PolicyRulesEditorProps {
  policyId: string;
  rules: PolicyRule[];
}

export function PolicyRulesEditor({ policyId, rules }: PolicyRulesEditorProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [newRule, setNewRule] = useState({
    conditionType: 'Attribute',
    field: SUPPORTED_FIELDS[0],
    operator: 'Equals',
    value: '',
    logicalGroup: 'And',
  });

  async function handleAddRule(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    try {
      const res = await fetch(`/api/policies/${policyId}/rules`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newRule),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setError(data.error || `Failed (${res.status})`);
        return;
      }

      startTransition(() => {
        setShowForm(false);
        setNewRule({ conditionType: 'Attribute', field: SUPPORTED_FIELDS[0], operator: 'Equals', value: '', logicalGroup: 'And' });
        router.refresh();
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    }
  }

  async function handleDeleteRule(ruleId: string) {
    try {
      const res = await fetch(`/api/policies/${policyId}/rules/${ruleId}`, { method: 'DELETE' });
      if (!res.ok) return;
      startTransition(() => router.refresh());
    } catch { /* ignore */ }
  }

  return (
    <div className="space-y-3">
      {rules.length === 0 && !showForm && (
        <div className="text-center py-8 text-gray-500 text-sm">
          No rules defined. This policy will allow all requests by default.
        </div>
      )}

      {rules.length > 0 && (
        <div className="space-y-2">
          {rules.map((rule, idx) => (
            <div key={rule.id} className="flex items-center gap-2 bg-gray-50 border border-gray-200 rounded-lg px-4 py-2.5 text-sm">
              {idx > 0 && (
                <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-bold ${
                  rule.logicalGroup === 'Or' ? 'bg-amber-100 text-amber-700' : 'bg-gray-200 text-gray-600'
                }`}>
                  {rule.logicalGroup.toUpperCase()}
                </span>
              )}
              <span className="text-gray-400 text-xs">{rule.conditionType}</span>
              <span className="font-mono text-indigo-700">{rule.field}</span>
              <span className="font-semibold text-gray-900">{formatOperator(rule.op)}</span>
              <span className="font-mono text-green-700">{rule.value}</span>
              <span className="flex-1" />
              <button
                onClick={() => handleDeleteRule(rule.id)}
                className="text-red-400 hover:text-red-600 text-xs transition-colors"
                disabled={isPending}
              >
                <i className="ri-delete-bin-line" />
              </button>
            </div>
          ))}
        </div>
      )}

      {!showForm ? (
        <button
          onClick={() => setShowForm(true)}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md border border-gray-300 text-gray-700 text-xs font-medium hover:bg-gray-50 transition-colors"
        >
          <i className="ri-add-line text-sm" />
          Add Rule
        </button>
      ) : (
        <form onSubmit={handleAddRule} className="bg-white border border-gray-200 rounded-lg p-4 space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Condition Type</label>
              <select
                value={newRule.conditionType}
                onChange={e => setNewRule(r => ({ ...r, conditionType: e.target.value }))}
                className="w-full px-2.5 py-1.5 border border-gray-300 rounded-md text-sm"
              >
                {CONDITION_TYPES.map(ct => <option key={ct} value={ct}>{ct}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Field</label>
              <select
                value={newRule.field}
                onChange={e => setNewRule(r => ({ ...r, field: e.target.value }))}
                className="w-full px-2.5 py-1.5 border border-gray-300 rounded-md text-sm"
              >
                {SUPPORTED_FIELDS.map(f => <option key={f} value={f}>{f}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Operator</label>
              <select
                value={newRule.operator}
                onChange={e => setNewRule(r => ({ ...r, operator: e.target.value }))}
                className="w-full px-2.5 py-1.5 border border-gray-300 rounded-md text-sm"
              >
                {OPERATORS.map(op => <option key={op} value={op}>{formatOperator(op)}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Logical Group</label>
              <select
                value={newRule.logicalGroup}
                onChange={e => setNewRule(r => ({ ...r, logicalGroup: e.target.value }))}
                className="w-full px-2.5 py-1.5 border border-gray-300 rounded-md text-sm"
              >
                {LOGICAL_GROUPS.map(lg => <option key={lg} value={lg}>{lg}</option>)}
              </select>
            </div>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-700 mb-1">Value</label>
            <input
              type="text"
              value={newRule.value}
              onChange={e => setNewRule(r => ({ ...r, value: e.target.value }))}
              placeholder='e.g. 50000 or ["CA","NV"]'
              className="w-full px-2.5 py-1.5 border border-gray-300 rounded-md text-sm"
              required
            />
          </div>

          {error && (
            <div className="bg-red-50 border border-red-200 rounded px-3 py-2 text-xs text-red-700">{error}</div>
          )}

          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => { setShowForm(false); setError(null); }} className="px-3 py-1.5 text-xs text-gray-600">
              Cancel
            </button>
            <button type="submit" disabled={isPending} className="px-4 py-1.5 bg-indigo-600 text-white text-xs font-medium rounded-md hover:bg-indigo-700 disabled:opacity-50">
              {isPending ? 'Adding...' : 'Add Rule'}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}

function formatOperator(op: string): string {
  const map: Record<string, string> = {
    Equals: '==',
    NotEquals: '!=',
    GreaterThan: '>',
    GreaterThanOrEqual: '>=',
    LessThan: '<',
    LessThanOrEqual: '<=',
    In: 'IN',
    NotIn: 'NOT IN',
    Contains: 'CONTAINS',
    StartsWith: 'STARTS WITH',
  };
  return map[op] ?? op;
}
