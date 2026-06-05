/**
 * TEMPORARY VISUAL REVIEW MOCK DATA — Synq Liens Case Section
 *
 * This module provides fallback display data for visual layout review only.
 * It does NOT replace real API/service logic and should NOT become a
 * production data source. Use only to fill empty/sparse UI sections
 * during visual review of the Case section body redesign.
 *
 * Remove this file once visual review is complete and real data is wired.
 */

export interface MockActivityItem {
  id: string;
  type: 'status_change' | 'note_added' | 'document_uploaded' | 'email_sent' | 'lien_added';
  description: string;
  user: string;
  timestamp: string;
  icon: string;
}

export interface MockNotePreview {
  id: string;
  content: string;
  author: string;
  createdAt: string;
}

export interface MockTask {
  id: string;
  title: string;
  status: 'pending' | 'in_progress' | 'completed';
  assignee: string;
  dueDate: string;
}

export interface MockContact {
  id: string;
  name: string;
  role: string;
  phone: string;
  email: string;
}

export const MOCK_ACTIVITY: MockActivityItem[] = [
  {
    id: 'act-1',
    type: 'status_change',
    description: 'Case status changed to In Negotiation',
    user: 'Sarah Mitchell',
    timestamp: '2026-04-14T10:32:00Z',
    icon: 'ri-arrow-right-circle-line',
  },
  {
    id: 'act-2',
    type: 'document_uploaded',
    description: 'Medical records uploaded (3 files)',
    user: 'James Rodriguez',
    timestamp: '2026-04-13T15:18:00Z',
    icon: 'ri-file-upload-line',
  },
  {
    id: 'act-3',
    type: 'note_added',
    description: 'Follow-up note added regarding settlement offer',
    user: 'Sarah Mitchell',
    timestamp: '2026-04-12T09:45:00Z',
    icon: 'ri-sticky-note-line',
  },
  {
    id: 'act-4',
    type: 'email_sent',
    description: 'Demand letter sent to insurance carrier',
    user: 'Michael Chen',
    timestamp: '2026-04-10T14:22:00Z',
    icon: 'ri-mail-send-line',
  },
  {
    id: 'act-5',
    type: 'lien_added',
    description: 'New medical lien added — $12,450.00',
    user: 'James Rodriguez',
    timestamp: '2026-04-08T11:05:00Z',
    icon: 'ri-add-circle-line',
  },
];

export const MOCK_NOTES: MockNotePreview[] = [
  {
    id: 'note-1',
    content: 'Spoke with carrier rep regarding updated demand. They indicated a counter-offer will be sent by EOW.',
    author: 'Sarah Mitchell',
    createdAt: '2026-04-12T09:45:00Z',
  },
  {
    id: 'note-2',
    content: 'Client confirmed receipt of medical records request. Awaiting provider response.',
    author: 'James Rodriguez',
    createdAt: '2026-04-09T16:30:00Z',
  },
];

export const MOCK_TASKS: MockTask[] = [
  {
    id: 'task-1',
    title: 'Review counter-offer from carrier',
    status: 'pending',
    assignee: 'Sarah Mitchell',
    dueDate: '2026-04-18',
  },
  {
    id: 'task-2',
    title: 'Follow up on outstanding medical records',
    status: 'in_progress',
    assignee: 'James Rodriguez',
    dueDate: '2026-04-16',
  },
  {
    id: 'task-3',
    title: 'Send updated lien verification letter',
    status: 'completed',
    assignee: 'Michael Chen',
    dueDate: '2026-04-11',
  },
];

export const MOCK_CONTACTS: MockContact[] = [
  {
    id: 'contact-1',
    name: 'Sarah Mitchell',
    role: 'Case Manager',
    phone: '(555) 234-5678',
    email: 'smitchell@lawfirm.com',
  },
  {
    id: 'contact-2',
    name: 'James Rodriguez',
    role: 'Lien Specialist',
    phone: '(555) 345-6789',
    email: 'jrodriguez@lawfirm.com',
  },
  {
    id: 'contact-3',
    name: 'Dr. Angela Watson',
    role: 'Treating Physician',
    phone: '(555) 456-7890',
    email: 'awatson@clinic.com',
  },
];

export const MOCK_CASE_SUMMARY = {
  totalLiens: 4,
  totalLienAmount: 47850.0,
  documentsCount: 12,
  openTasksCount: 2,
  lastActivityDate: '2026-04-14T10:32:00Z',
};

export function formatMockDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  } catch {
    return iso;
  }
}

export function formatMockDateTime(iso: string): string {
  try {
    const d = new Date(iso);
    return `${d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })} at ${d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', hour12: true })}`;
  } catch {
    return iso;
  }
}
