'use client';

import { useCallback, useState } from 'react';
import { MyWorkClient } from './my-work-client';
import { OrgQueueClient } from './org-queue-client';
import { RoleQueueClient } from './role-queue-client';
import { TaskDetailDrawer } from './task-detail-drawer';

/**
 * LS-FLOW-E15 — root client for the consolidated **Work** area.
 *
 * <p>Renders a single tabbed surface so the user can switch between
 * direct work (My Tasks), role-based queue work, and org-based queue
 * work without leaving the page. Per the spec's recommendation we
 * use one route with tabs rather than three sibling pages.</p>
 *
 * <p><b>Capability-aware tabs:</b> the Role Queue and Org Queue tabs
 * are hidden when the calling user has no roles / no org and is not
 * a platform admin. The backend would return an empty list either
 * way; hiding the empty surface avoids the misleading "no results"
 * implication.</p>
 *
 * <p><b>Drawer wiring:</b> any row click in any tab opens the shared
 * task-detail drawer. After a claim or reassign in the drawer (or
 * inline on a queue row), we bump <code>refreshKey</code> which
 * causes every mounted list to refetch — the cheapest correct way
 * to keep all three lists honest after a mutation that may move a
 * task between them.</p>
 */
export interface WorkAreaClientProps {
  showRoleQueue: boolean;
  showOrgQueue: boolean;
  canReassign: boolean;
}

type TabKey = 'mine' | 'role' | 'org';

export function WorkAreaClient({
  showRoleQueue,
  showOrgQueue,
  canReassign,
}: WorkAreaClientProps) {
  const [tab, setTab] = useState<TabKey>('mine');
  const [openTaskId, setOpenTaskId] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const bumpRefresh = useCallback(() => {
    setRefreshKey((k) => k + 1);
  }, []);

  const openTask = useCallback((id: string) => setOpenTaskId(id), []);
  const closeDrawer = useCallback(() => setOpenTaskId(null), []);

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-gray-900">Work</h1>
        <p className="text-sm text-gray-500">
          Tasks assigned to you and queues you can claim from.
        </p>
      </div>

      <div className="border-b border-gray-200">
        <nav className="-mb-px flex gap-6" aria-label="Work area tabs">
          <TabButton active={tab === 'mine'} onClick={() => setTab('mine')}>
            My Tasks
          </TabButton>
          {showRoleQueue && (
            <TabButton active={tab === 'role'} onClick={() => setTab('role')}>
              Role queue
            </TabButton>
          )}
          {showOrgQueue && (
            <TabButton active={tab === 'org'} onClick={() => setTab('org')}>
              Org queue
            </TabButton>
          )}
        </nav>
      </div>

      <div>
        {tab === 'mine' && (
          <MyWorkClient
            key={`mine-${refreshKey}`}
            onOpenTask={openTask}
          />
        )}
        {tab === 'role' && showRoleQueue && (
          <RoleQueueClient
            onOpenTask={openTask}
            refreshKey={refreshKey}
          />
        )}
        {tab === 'org' && showOrgQueue && (
          <OrgQueueClient
            onOpenTask={openTask}
            refreshKey={refreshKey}
          />
        )}
      </div>

      <TaskDetailDrawer
        taskId={openTaskId}
        canReassign={canReassign}
        onClose={closeDrawer}
        onTaskMutated={bumpRefresh}
      />
    </div>
  );
}

function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={
        'px-1 pb-3 text-sm font-medium border-b-2 transition-colors ' +
        (active
          ? 'border-indigo-600 text-indigo-600'
          : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300')
      }
      aria-current={active ? 'page' : undefined}
    >
      {children}
    </button>
  );
}
