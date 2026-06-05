'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import type { GroupProductAccess } from '@/types/control-center';

const KNOWN_PRODUCTS = ['FUND', 'CARECONNECT', 'DOCUMENTS', 'NOTIFICATIONS'];

interface GroupProductAccessPanelProps {
  tenantId: string;
  groupId:  string;
  products: GroupProductAccess[];
}

function fmtDate(iso: string): string {
  try { return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }); }
  catch { return iso; }
}

export function GroupProductAccessPanel({ tenantId, groupId, products }: GroupProductAccessPanelProps) {
  const router = useRouter();

  const [productCode, setProductCode] = useState('');
  const [granting, setGranting]       = useState(false);
  const [grantError, setGrantError]   = useState<string | null>(null);
  const [grantOk, setGrantOk]         = useState(false);

  const [revokeConfirm, setRevokeConfirm] = useState<string | null>(null);
  const [revoking, setRevoking]           = useState<string | null>(null);
  const [revokeError, setRevokeError]     = useState<string | null>(null);

  useEffect(() => {
    if (!grantOk) return;
    const t = setTimeout(() => setGrantOk(false), 3000);
    return () => clearTimeout(t);
  }, [grantOk]);

  const grantedCodes = new Set(products.map(p => p.productCode));
  const availableProducts = KNOWN_PRODUCTS.filter(p => !grantedCodes.has(p));

  async function handleGrant() {
    if (!productCode) return;
    setGranting(true);
    setGrantError(null);
    setGrantOk(false);
    try {
      const res = await fetch(
        `/api/access-groups/${encodeURIComponent(tenantId)}/${encodeURIComponent(groupId)}/products/${encodeURIComponent(productCode)}`,
        { method: 'PUT' },
      );
      if (!res.ok) {
        const data = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(data.message ?? 'Failed to grant product.');
      }
      setGrantOk(true);
      setProductCode('');
      router.refresh();
    } catch (err) {
      setGrantError(err instanceof Error ? err.message : 'An error occurred.');
    } finally {
      setGranting(false);
    }
  }

  async function handleRevoke(code: string) {
    setRevoking(code);
    setRevokeError(null);
    try {
      const res = await fetch(
        `/api/access-groups/${encodeURIComponent(tenantId)}/${encodeURIComponent(groupId)}/products/${encodeURIComponent(code)}`,
        { method: 'DELETE' },
      );
      if (!res.ok) {
        const data = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(data.message ?? 'Failed to revoke product.');
      }
      router.refresh();
    } catch (err) {
      setRevokeError(err instanceof Error ? err.message : 'An error occurred.');
    } finally {
      setRevoking(null);
      setRevokeConfirm(null);
    }
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">Product Access</h2>
          <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold border bg-purple-50 text-purple-600 border-purple-200">
            {products.length}
          </span>
        </div>
        <span className="text-[11px] text-gray-400">Inherited by all members</span>
      </div>

      {products.length > 0 ? (
        <ul className="divide-y divide-gray-100">
          {products.map(p => (
            <li key={p.productCode} className="flex items-center justify-between px-5 py-2.5 gap-3">
              <div className="flex items-center gap-3">
                <span className="font-mono text-sm font-medium text-gray-800">{p.productCode}</span>
                <span className="text-[11px] text-gray-400">
                  Granted {fmtDate(p.grantedAtUtc)}
                </span>
              </div>

              {revokeConfirm === p.productCode ? (
                <span className="inline-flex items-center gap-1 text-xs shrink-0">
                  <span className="text-red-700 font-medium">Revoke?</span>
                  <button
                    type="button"
                    disabled={revoking === p.productCode}
                    onClick={() => handleRevoke(p.productCode)}
                    className="px-2 py-0.5 rounded bg-red-600 text-white text-[11px] font-medium hover:bg-red-700 disabled:opacity-50 transition-colors"
                  >
                    {revoking === p.productCode ? '…' : 'Yes'}
                  </button>
                  <button
                    type="button"
                    onClick={() => setRevokeConfirm(null)}
                    className="px-2 py-0.5 rounded border border-gray-200 bg-white text-gray-500 text-[11px] hover:bg-gray-50 transition-colors"
                  >
                    Cancel
                  </button>
                </span>
              ) : (
                <button
                  type="button"
                  disabled={revoking !== null}
                  onClick={() => setRevokeConfirm(p.productCode)}
                  className="text-xs px-2 py-1 rounded border border-red-200 bg-white text-red-600 hover:bg-red-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed shrink-0"
                >
                  Revoke
                </button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <div className="px-5 py-6 text-center">
          <p className="text-sm font-medium text-gray-500">No products granted</p>
          <p className="text-xs text-gray-400 mt-1">Grant a product to give all members access.</p>
        </div>
      )}

      {revokeError && (
        <div className="mx-5 my-2 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
          {revokeError}
        </div>
      )}

      {availableProducts.length > 0 && (
        <div className="px-5 py-3 border-t border-gray-100 bg-gray-50 flex items-end gap-3 flex-wrap">
          <div className="flex-1 min-w-36">
            <label className="block text-xs font-medium text-gray-600 mb-1">Grant product</label>
            <select
              value={productCode}
              onChange={e => { setProductCode(e.target.value); setGrantOk(false); setGrantError(null); }}
              className="w-full h-8 rounded-md border border-gray-300 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white"
            >
              <option value="">Select product…</option>
              {availableProducts.map(p => (
                <option key={p} value={p}>{p}</option>
              ))}
            </select>
          </div>
          <button
            type="button"
            disabled={!productCode || granting}
            onClick={handleGrant}
            className="h-8 px-4 text-sm font-medium text-white bg-purple-600 hover:bg-purple-700 rounded-md transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {granting ? 'Granting…' : 'Grant'}
          </button>
          {grantOk && (
            <span className="text-xs text-green-700 font-medium flex items-center gap-1">
              <span className="w-1.5 h-1.5 rounded-full bg-green-500 inline-block" />
              Granted.
            </span>
          )}
        </div>
      )}

      {grantError && (
        <div className="mx-5 mb-3 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
          {grantError}
        </div>
      )}
    </div>
  );
}
