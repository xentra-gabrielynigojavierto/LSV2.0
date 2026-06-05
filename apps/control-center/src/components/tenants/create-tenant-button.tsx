'use client';

import { useState }              from 'react';
import { CreateTenantModal }     from './create-tenant-modal';

export function CreateTenantButton() {
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="bg-indigo-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-indigo-700 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1"
      >
        Create Tenant
      </button>

      {open && <CreateTenantModal onClose={() => setOpen(false)} />}
    </>
  );
}
