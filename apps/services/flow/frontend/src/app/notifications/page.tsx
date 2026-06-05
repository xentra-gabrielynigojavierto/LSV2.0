"use client";

import { NotificationList } from "@/components/notifications/NotificationList";
import { ErrorBoundary } from "@/components/ui/ErrorBoundary";
import { TenantSwitcher } from "@/components/ui/TenantSwitcher";
import { NavLinks } from "@/components/ui/NavLinks";

export default function NotificationsPage() {
  return (
    <ErrorBoundary>
      <div className="min-h-screen bg-gray-50">
        <header className="border-b border-gray-200 bg-white">
          <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8">
            <div className="flex h-14 items-center justify-between">
              <div className="flex items-center gap-3">
                <a href="/" className="text-gray-400 hover:text-gray-600 text-sm">
                  Flow
                </a>
                <span className="text-gray-300">/</span>
                <h1 className="text-lg font-semibold text-gray-900">Notifications</h1>
              </div>
              <div className="flex items-center gap-4">
                <TenantSwitcher onTenantChange={() => window.location.reload()} />
                <span className="h-4 w-px bg-gray-200" />
                <NavLinks current="notifications" />
              </div>
            </div>
          </div>
        </header>

        <main className="mx-auto max-w-4xl px-4 py-6 sm:px-6 lg:px-8">
          <NotificationList />
        </main>
      </div>
    </ErrorBoundary>
  );
}
