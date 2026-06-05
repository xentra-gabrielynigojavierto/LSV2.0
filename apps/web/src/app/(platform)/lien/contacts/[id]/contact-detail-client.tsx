'use client';

import { useState, useEffect, useCallback } from 'react';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { CONTACT_TYPE_LABELS } from '@/types/lien';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { ConfirmDialog } from '@/components/lien/modal';
import { EntityTimeline } from '@/components/lien/entity-timeline';
import { contactsService, type ContactDetail } from '@/lib/contacts';

export function ContactDetailClient({ id }: { id: string }) {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const [contact, setContact] = useState<ContactDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [confirmAction, setConfirmAction] = useState<{ action: string; label: string } | null>(null);

  const fetchContact = useCallback(async () => {
    try {
      setLoading(true);
      const detail = await contactsService.getContact(id);
      setContact(detail);
    } catch (err) {
      addToast({ type: 'error', title: 'Load Failed', description: err instanceof Error ? err.message : 'Failed to load contact' });
    } finally {
      setLoading(false);
    }
  }, [id, addToast]);

  useEffect(() => { fetchContact(); }, [fetchContact]);

  const canEdit = ra.can('contact:edit');

  const handleStatusToggle = async () => {
    if (!confirmAction || !contact) return;
    try {
      const updated = confirmAction.action === 'deactivate'
        ? await contactsService.deactivateContact(id)
        : await contactsService.reactivateContact(id);
      setContact(updated);
      addToast({ type: 'success', title: confirmAction.label });
      setConfirmAction(null);
    } catch (err) {
      addToast({ type: 'error', title: 'Action Failed', description: err instanceof Error ? err.message : 'Failed to update contact status' });
      setConfirmAction(null);
    }
  };

  if (loading) return <div className="p-10 text-center text-sm text-gray-400">Loading contact...</div>;
  if (!contact) return <div className="p-10 text-center text-gray-400">Contact not found.</div>;

  return (
    <div className="space-y-5">
      <DetailHeader title={contact.displayName} subtitle={contact.organization}
        badge={<span className="inline-flex items-center rounded-full border px-2.5 py-1 text-sm font-medium bg-gray-50 text-gray-600 border-gray-200">{CONTACT_TYPE_LABELS[contact.contactType] ?? contact.contactType}</span>}
        backHref="/lien/contacts" backLabel="Back to Contacts"
        meta={[
          ...(contact.title ? [{ label: 'Title', value: contact.title }] : []),
          { label: 'Status', value: contact.isActive ? 'Active' : 'Inactive' },
          { label: 'Member Since', value: contact.createdAt },
        ]}
        actions={canEdit ? (
          <div className="flex gap-2">
            {contact.email && (
              <a href={`mailto:${contact.email}`} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Send Email</a>
            )}
            {contact.isActive ? (
              <button onClick={() => setConfirmAction({ action: 'deactivate', label: 'Deactivate Contact' })} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Deactivate</button>
            ) : (
              <button onClick={() => setConfirmAction({ action: 'reactivate', label: 'Reactivate Contact' })} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Reactivate</button>
            )}
          </div>
        ) : undefined}
      />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="Contact Information" icon="ri-contacts-book-line" fields={[
          { label: 'Email', value: contact.email },
          { label: 'Phone', value: contact.phone },
          { label: 'Fax', value: contact.fax },
          { label: 'Website', value: contact.website ? <a href={contact.website} target="_blank" rel="noopener noreferrer" className="text-primary hover:underline">{contact.website}</a> : undefined },
        ]} />
        <DetailSection title="Location" icon="ri-map-pin-line" fields={[
          { label: 'Address', value: contact.addressLine1 },
          { label: 'City', value: contact.city },
          { label: 'State', value: contact.state },
          { label: 'ZIP Code', value: contact.postalCode },
        ]} />
      </div>

      {contact.notes && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Notes</h3>
          <p className="text-sm text-gray-600">{contact.notes}</p>
        </div>
      )}

      <EntityTimeline entityType="Contact" entityId={id} />

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={handleStatusToggle}
          title={confirmAction.label} description={`${confirmAction.label} for ${contact.displayName}?`} confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
