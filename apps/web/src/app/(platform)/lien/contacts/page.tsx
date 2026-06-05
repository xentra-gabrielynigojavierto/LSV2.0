'use client';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { ActionMenu } from '@/components/lien/action-menu';
import { SideDrawer } from '@/components/lien/side-drawer';
import { AddContactForm } from '@/components/lien/forms/add-contact-form';
import { BulkActionBar } from '@/components/lien/bulk-action-bar';
import { BulkConfirmModal } from '@/components/lien/bulk-confirm-modal';
import { BulkResultBanner } from '@/components/lien/bulk-result-banner';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { useSelectionState } from '@/hooks/use-selection-state';
import { CONTACT_TYPE_LABELS } from '@/types/lien';
import { contactsService, type ContactListItem } from '@/lib/contacts';
import { executeBulk, type BulkActionConfig, type BulkOperationResult } from '@/lib/bulk-operations';

export const dynamic = 'force-dynamic';


const BULK_ACTIONS: BulkActionConfig[] = [
  {
    key: 'deactivate',
    label: 'Deactivate',
    icon: 'ri-user-unfollow-line',
    variant: 'danger',
    confirmTitle: 'Deactivate Contacts',
    confirmDescription: (count) =>
      `This will deactivate ${count} contact${count !== 1 ? 's' : ''}. Already inactive contacts will be skipped.`,
  },
];

export default function ContactsPage() {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const selection = useSelectionState();

  const [contacts, setContacts] = useState<ContactListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [previewId, setPreviewId] = useState<string | null>(null);

  const [bulkAction, setBulkAction] = useState<BulkActionConfig | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [bulkResult, setBulkResult] = useState<BulkOperationResult | null>(null);

  const fetchContacts = useCallback(async () => {
    try {
      setLoading(true);
      const result = await contactsService.getContacts({
        search: search || undefined,
        contactType: typeFilter || undefined,
        pageSize: 100,
      });
      setContacts(result.items);
      setTotalCount(result.pagination.totalCount);
    } catch (err) {
      addToast({ type: 'error', title: 'Load Failed', description: err instanceof Error ? err.message : 'Failed to load contacts' });
    } finally {
      setLoading(false);
    }
  }, [search, typeFilter, addToast]);

  useEffect(() => { fetchContacts(); }, [fetchContacts]);

  const previewContact = previewId ? contacts.find((c) => c.id === previewId) : null;
  const canEdit = ra.can('contact:edit');

  const handleBulkAction = (actionKey: string) => {
    const action = BULK_ACTIONS.find((a) => a.key === actionKey);
    if (action) setBulkAction(action);
  };

  const executeBulkAction = async () => {
    if (!bulkAction) return;
    setBulkLoading(true);
    const result = await executeBulk(selection.ids, async (id) => {
      const contact = contacts.find((c) => c.id === id);
      if (!contact) throw new Error('Contact not found in current list');
      if (!contact.isActive) throw new Error('Contact is already inactive');
      await contactsService.deactivateContact(id);
    });
    setBulkLoading(false);
    setBulkAction(null);
    setBulkResult(result);
    selection.clear();
    fetchContacts();
  };

  const allIds = contacts.map((c) => c.id);

  return (
    <div className="space-y-5">
      <PageHeader title="Contacts" subtitle={`${totalCount} contacts`}
        actions={ra.can('contact:create') ? (
          <button onClick={() => setShowCreate(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />Add Contact
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search contacts by name, org, or email..." onSearch={setSearch} filters={[
        { label: 'All Types', value: typeFilter, onChange: setTypeFilter, options: Object.entries(CONTACT_TYPE_LABELS).map(([v, l]) => ({ value: v, label: l })) },
      ]} />

      <BulkResultBanner result={bulkResult} onDismiss={() => setBulkResult(null)} entityLabel="contacts" />

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {loading ? (
          <div className="p-10 text-center text-sm text-gray-400">Loading contacts...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100">
              <thead><tr className="bg-gray-50">
                {canEdit && (
                  <th className="px-4 py-3 w-10">
                    <input type="checkbox" checked={selection.isAllSelected(allIds)} onChange={() => selection.toggleAll(allIds)}
                      className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary/20" />
                  </th>
                )}
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Organization</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Email</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Phone</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Location</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3" />
              </tr></thead>
              <tbody className="divide-y divide-gray-100">
                {contacts.map((c) => (
                  <tr key={c.id} className={`hover:bg-gray-50 transition-colors cursor-pointer ${selection.isSelected(c.id) ? 'bg-primary/5' : ''}`} onClick={() => setPreviewId(c.id)}>
                    {canEdit && (
                      <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                        <input type="checkbox" checked={selection.isSelected(c.id)} onChange={() => selection.toggle(c.id)}
                          className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary/20" />
                      </td>
                    )}
                    <td className="px-4 py-3"><Link href={`/lien/contacts/${c.id}`} onClick={(e) => e.stopPropagation()} className="text-sm font-medium text-gray-700 hover:text-primary">{c.displayName}</Link></td>
                    <td className="px-4 py-3"><span className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium bg-gray-50 text-gray-600 border-gray-200">{CONTACT_TYPE_LABELS[c.contactType] ?? c.contactType}</span></td>
                    <td className="px-4 py-3 text-sm text-gray-600">{c.organization}</td>
                    <td className="px-4 py-3 text-sm text-gray-500">{c.email}</td>
                    <td className="px-4 py-3 text-sm text-gray-500">{c.phone}</td>
                    <td className="px-4 py-3 text-sm text-gray-500">{c.city}{c.city && c.state ? ', ' : ''}{c.state}</td>
                    <td className="px-4 py-3"><span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${c.isActive ? 'bg-green-50 text-green-700 border border-green-200' : 'bg-gray-100 text-gray-500 border border-gray-200'}`}>{c.isActive ? 'Active' : 'Inactive'}</span></td>
                    <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                      <ActionMenu items={[
                        { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                        ...(c.email ? [{ label: 'Send Email', icon: 'ri-mail-line', onClick: () => addToast({ type: 'info', title: 'Email', description: `Opening email to ${c.email}` }) }] : []),
                      ]} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {!loading && contacts.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No contacts found.</div>}
      </div>

      {canEdit && (
        <BulkActionBar count={selection.count} actions={BULK_ACTIONS} onAction={handleBulkAction} onClear={selection.clear} />
      )}

      {bulkAction && (
        <BulkConfirmModal
          open
          onClose={() => setBulkAction(null)}
          onConfirm={executeBulkAction}
          title={bulkAction.confirmTitle}
          description={bulkAction.confirmDescription(selection.count)}
          count={selection.count}
          variant={bulkAction.variant}
          loading={bulkLoading}
        />
      )}

      <AddContactForm open={showCreate} onClose={() => setShowCreate(false)} onCreated={fetchContacts} />

      <SideDrawer open={!!previewContact} onClose={() => setPreviewId(null)} title={previewContact?.displayName || ''} subtitle={previewContact?.organization}>
        {previewContact && (
          <div className="space-y-4">
            <span className="inline-flex items-center rounded-full border px-2.5 py-1 text-sm font-medium bg-gray-50 text-gray-600 border-gray-200">{CONTACT_TYPE_LABELS[previewContact.contactType] ?? previewContact.contactType}</span>
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div><p className="text-xs text-gray-400">Email</p><p className="text-gray-700">{previewContact.email || '\u2014'}</p></div>
              <div><p className="text-xs text-gray-400">Phone</p><p className="text-gray-700">{previewContact.phone || '\u2014'}</p></div>
              <div><p className="text-xs text-gray-400">Location</p><p className="text-gray-700">{previewContact.city}{previewContact.city && previewContact.state ? ', ' : ''}{previewContact.state || '\u2014'}</p></div>
              <div><p className="text-xs text-gray-400">Status</p><p className="text-gray-700">{previewContact.isActive ? 'Active' : 'Inactive'}</p></div>
            </div>
            <Link href={`/lien/contacts/${previewContact.id}`} className="block text-center text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">View Full Details</Link>
          </div>
        )}
      </SideDrawer>
    </div>
  );
}
