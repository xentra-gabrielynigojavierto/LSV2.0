'use client';

import { useState } from 'react';
import { useLienStore, type AppRole } from '@/stores/lien-store';

const ROLES: { value: AppRole; label: string; icon: string; color: string }[] = [
  { value: 'Admin', label: 'Admin', icon: 'ri-shield-user-line', color: 'text-red-600' },
  { value: 'Case Manager', label: 'Case Manager', icon: 'ri-user-settings-line', color: 'text-blue-600' },
  { value: 'Analyst', label: 'Analyst', icon: 'ri-line-chart-line', color: 'text-amber-600' },
  { value: 'Viewer', label: 'Viewer', icon: 'ri-eye-line', color: 'text-gray-500' },
];

export function RoleSwitcher() {
  const [open, setOpen] = useState(false);
  const currentRole = useLienStore((s) => s.currentRole);
  const setCurrentRole = useLienStore((s) => s.setCurrentRole);
  const current = ROLES.find((r) => r.value === currentRole) || ROLES[0];

  return (
    <div className="fixed bottom-4 left-4 z-[55]">
      {open && (
        <div className="mb-2 bg-white border border-gray-200 rounded-xl shadow-xl overflow-hidden animate-in slide-in-from-bottom duration-200">
          <div className="px-3 py-2 border-b border-gray-100">
            <p className="text-xs font-medium text-gray-500">Simulate Role</p>
          </div>
          {ROLES.map((role) => (
            <button
              key={role.value}
              onClick={() => { setCurrentRole(role.value); setOpen(false); }}
              className={`w-full flex items-center gap-2 px-3 py-2 text-sm text-left hover:bg-gray-50 transition-colors ${
                currentRole === role.value ? 'bg-primary/5 font-medium' : ''
              }`}
            >
              <i className={`${role.icon} text-base ${role.color}`} />
              <span className="text-gray-700">{role.label}</span>
              {currentRole === role.value && <i className="ri-check-line text-primary ml-auto" />}
            </button>
          ))}
        </div>
      )}
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-2 px-3 py-2 bg-white border border-gray-200 rounded-xl shadow-lg hover:shadow-xl transition-all text-sm"
      >
        <i className={`${current.icon} text-base ${current.color}`} />
        <span className="text-gray-700 font-medium">{current.label}</span>
        <i className={`ri-arrow-${open ? 'down' : 'up'}-s-line text-gray-400`} />
      </button>
    </div>
  );
}
