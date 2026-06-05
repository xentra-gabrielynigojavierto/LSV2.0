'use client';

import { type ReactNode } from 'react';
import { ToastContainer } from './toast-container';
import { RoleSwitcher } from './role-switcher';

export function LienProviders({ children }: { children: ReactNode }) {
  return (
    <>
      {children}
      <ToastContainer />
      <RoleSwitcher />
    </>
  );
}
