'use client';

import { useState } from 'react';
import { StatusSummaryBanner } from './status-summary-banner';
import { ComponentStatusList } from './component-status-list';
import type { MonitoringStatus, IntegrationStatus, SystemAlert } from '@/types/control-center';

export type StatusFilter = 'all' | 'healthy' | 'degraded' | 'down' | 'alerts';

interface MonitoringFilterSectionProps {
  systemStatus:  MonitoringStatus;
  total:         number;
  healthy:       number;
  degraded:      number;
  down:          number;
  lastCheckedAt: string;
  integrations:  IntegrationStatus[];
  alerts:        SystemAlert[];
}

/**
 * MonitoringFilterSection — client wrapper that owns the shared statusFilter state.
 *
 * Sits between the server-rendered MonitoringPage and the two interactive
 * components that need to share filter state:
 *   - StatusSummaryBanner (click a status segment to set the filter)
 *   - ComponentStatusList (applies the filter; own buttons stay in sync)
 *
 * No API calls are made here; all data comes from the server page.
 */
export function MonitoringFilterSection({
  systemStatus,
  total,
  healthy,
  degraded,
  down,
  lastCheckedAt,
  integrations,
  alerts,
}: MonitoringFilterSectionProps) {
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');

  return (
    <>
      <StatusSummaryBanner
        systemStatus={systemStatus}
        total={total}
        healthy={healthy}
        degraded={degraded}
        down={down}
        alerts={alerts.length}
        lastCheckedAt={lastCheckedAt}
        statusFilter={statusFilter}
        onFilterChange={setStatusFilter}
      />

      <ComponentStatusList
        integrations={integrations}
        alerts={alerts}
        externalFilter={statusFilter}
        onExternalFilterChange={setStatusFilter}
      />
    </>
  );
}
