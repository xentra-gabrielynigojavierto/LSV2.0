'use client';

import { useState } from 'react';
import type { RoleSummary } from '@/types/control-center';
import { InvitePlatformUserModal } from './invite-platform-user-modal';

interface InvitePlatformUserButtonProps {
  platformRoles: RoleSummary[];
}

export function InvitePlatformUserButton({ platformRoles }: InvitePlatformUserButtonProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        className="inline-flex items-center gap-1.5 text-sm px-3 py-1.5 rounded-md bg-indigo-600 text-white hover:bg-indigo-700 transition-colors shadow-sm"
      >
        <span className="ri-user-add-line text-base" />
        Invite Platform User
      </button>

      <InvitePlatformUserModal
        open={open}
        onClose={() => setOpen(false)}
        platformRoles={platformRoles}
      />
    </>
  );
}
