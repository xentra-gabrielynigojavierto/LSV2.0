'use client';

import { useState } from 'react';
import type { ExportFormat, DeliveryMethod } from '@/lib/reports/reports.types';

interface ScheduleFormData {
  scheduleName: string;
  frequency: 'daily' | 'weekly' | 'monthly';
  hour: string;
  minute: string;
  dayOfWeek: string;
  dayOfMonth: string;
  timezone: string;
  exportFormat: ExportFormat;
  deliveryMethod: DeliveryMethod;
  emailRecipients: string;
  sftpHost: string;
  sftpPath: string;
}

interface ScheduleFormProps {
  initial?: Partial<ScheduleFormData>;
  onSubmit: (data: ScheduleFormData, cronExpression: string) => Promise<void>;
  onCancel: () => void;
  submitLabel?: string;
}

const TIMEZONES = [
  'America/New_York',
  'America/Chicago',
  'America/Denver',
  'America/Los_Angeles',
  'America/Phoenix',
  'UTC',
];

export function ScheduleForm({ initial, onSubmit, onCancel, submitLabel = 'Create Schedule' }: ScheduleFormProps) {
  const [form, setForm] = useState<ScheduleFormData>({
    scheduleName: initial?.scheduleName ?? '',
    frequency: initial?.frequency ?? 'daily',
    hour: initial?.hour ?? '8',
    minute: initial?.minute ?? '0',
    dayOfWeek: initial?.dayOfWeek ?? '1',
    dayOfMonth: initial?.dayOfMonth ?? '1',
    timezone: initial?.timezone ?? 'America/New_York',
    exportFormat: initial?.exportFormat ?? 'CSV',
    deliveryMethod: initial?.deliveryMethod ?? 'OnScreen',
    emailRecipients: initial?.emailRecipients ?? '',
    sftpHost: initial?.sftpHost ?? '',
    sftpPath: initial?.sftpPath ?? '',
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function buildCron(): string {
    const min = form.minute;
    const hr = form.hour;
    if (form.frequency === 'daily') return `${min} ${hr} * * *`;
    if (form.frequency === 'weekly') return `${min} ${hr} * * ${form.dayOfWeek}`;
    return `${min} ${hr} ${form.dayOfMonth} * *`;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.scheduleName.trim()) {
      setError('Schedule name is required.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(form, buildCron());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save schedule');
    } finally {
      setSubmitting(false);
    }
  }

  function update<K extends keyof ScheduleFormData>(key: K, value: ScheduleFormData[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Schedule Name</label>
        <input
          type="text"
          value={form.scheduleName}
          onChange={(e) => update('scheduleName', e.target.value)}
          className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary"
          placeholder="e.g., Weekly Lien Summary"
        />
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Frequency</label>
          <select
            value={form.frequency}
            onChange={(e) => update('frequency', e.target.value as ScheduleFormData['frequency'])}
            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
          >
            <option value="daily">Daily</option>
            <option value="weekly">Weekly</option>
            <option value="monthly">Monthly</option>
          </select>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Time</label>
          <div className="flex items-center gap-2">
            <input
              type="number"
              min="0"
              max="23"
              value={form.hour}
              onChange={(e) => update('hour', e.target.value)}
              className="w-16 border border-gray-300 rounded-lg px-2 py-2 text-sm text-center"
            />
            <span className="text-gray-500">:</span>
            <input
              type="number"
              min="0"
              max="59"
              value={form.minute}
              onChange={(e) => update('minute', e.target.value)}
              className="w-16 border border-gray-300 rounded-lg px-2 py-2 text-sm text-center"
            />
          </div>
        </div>

        {form.frequency === 'weekly' && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Day of Week</label>
            <select
              value={form.dayOfWeek}
              onChange={(e) => update('dayOfWeek', e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
            >
              <option value="0">Sunday</option>
              <option value="1">Monday</option>
              <option value="2">Tuesday</option>
              <option value="3">Wednesday</option>
              <option value="4">Thursday</option>
              <option value="5">Friday</option>
              <option value="6">Saturday</option>
            </select>
          </div>
        )}

        {form.frequency === 'monthly' && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Day of Month</label>
            <input
              type="number"
              min="1"
              max="28"
              value={form.dayOfMonth}
              onChange={(e) => update('dayOfMonth', e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
            />
          </div>
        )}
      </div>

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Timezone</label>
        <select
          value={form.timezone}
          onChange={(e) => update('timezone', e.target.value)}
          className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
        >
          {TIMEZONES.map((tz) => (
            <option key={tz} value={tz}>{tz.replace('_', ' ')}</option>
          ))}
        </select>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Export Format</label>
          <select
            value={form.exportFormat}
            onChange={(e) => update('exportFormat', e.target.value as ExportFormat)}
            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
          >
            <option value="CSV">CSV</option>
            <option value="XLSX">Excel (XLSX)</option>
            <option value="PDF">PDF</option>
          </select>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Delivery Method</label>
          <select
            value={form.deliveryMethod}
            onChange={(e) => update('deliveryMethod', e.target.value as DeliveryMethod)}
            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
          >
            <option value="OnScreen">On Screen</option>
            <option value="Email">Email</option>
            <option value="SFTP">SFTP</option>
          </select>
        </div>
      </div>

      {form.deliveryMethod === 'Email' && (
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Email Recipients</label>
          <input
            type="text"
            value={form.emailRecipients}
            onChange={(e) => update('emailRecipients', e.target.value)}
            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
            placeholder="comma-separated emails"
          />
        </div>
      )}

      {form.deliveryMethod === 'SFTP' && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">SFTP Host</label>
            <input
              type="text"
              value={form.sftpHost}
              onChange={(e) => update('sftpHost', e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
              placeholder="sftp.example.com"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">SFTP Path</label>
            <input
              type="text"
              value={form.sftpPath}
              onChange={(e) => update('sftpPath', e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
              placeholder="/reports/"
            />
          </div>
        </div>
      )}

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      <div className="flex items-center justify-end gap-3 pt-2">
        <button
          type="button"
          onClick={onCancel}
          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
        >
          Cancel
        </button>
        <button
          type="submit"
          disabled={submitting}
          className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-50"
        >
          {submitting ? 'Saving...' : submitLabel}
        </button>
      </div>
    </form>
  );
}
