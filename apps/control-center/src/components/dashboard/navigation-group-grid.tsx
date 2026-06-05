'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { Suspense } from 'react';
import { getNavGroupModels, getSectionBySlug } from '@/lib/nav-utils';
import type { NavGroupModel } from '@/lib/nav-utils';

// ── Group card ───────────────────────────────────────────────────────────────

function GroupCard({
  group,
  isSelected,
  onSelect,
}: {
  group:      NavGroupModel;
  isSelected: boolean;
  onSelect:   () => void;
}) {
  return (
    <button
      onClick={onSelect}
      className={[
        'group text-left w-full rounded-xl border px-4 py-4 transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400',
        isSelected
          ? 'border-orange-300 bg-orange-50 shadow-sm'
          : 'border-gray-200 bg-white hover:border-gray-300 hover:shadow-sm',
      ].join(' ')}
      aria-pressed={isSelected}
      aria-label={`${group.heading} — ${group.itemCount} tools`}
    >
      <div className="flex items-start gap-3">
        <div
          className={[
            'shrink-0 w-8 h-8 rounded-lg flex items-center justify-center transition-colors',
            isSelected ? 'bg-orange-100' : 'bg-gray-100 group-hover:bg-gray-200',
          ].join(' ')}
        >
          <i
            className={`${group.icon} text-[16px] leading-none`}
            style={{ color: isSelected ? '#f97316' : undefined }}
          />
        </div>
        <div className="min-w-0 flex-1">
          <p
            className={[
              'text-[11px] font-bold uppercase tracking-wider truncate',
              isSelected ? 'text-orange-600' : 'text-gray-500 group-hover:text-gray-700',
            ].join(' ')}
          >
            {group.heading}
          </p>
          <p className="text-[12px] text-gray-400 mt-0.5">
            {group.itemCount} {group.itemCount === 1 ? 'tool' : 'tools'}
            {group.liveCount > 0 && (
              <span className="ml-1.5 text-emerald-600">· {group.liveCount} live</span>
            )}
          </p>
        </div>
        {/* Selected checkmark */}
        {isSelected && (
          <i className="ri-check-line text-orange-400 text-[15px] shrink-0 mt-0.5" />
        )}
      </div>
    </button>
  );
}

// ── Group summary panel — body context (sidebar owns the full menu list) ─────

function GroupSummaryPanel({ group }: { group: NavGroupModel }) {
  return (
    <div className="mt-5 rounded-xl border border-orange-200 bg-orange-50/40 px-5 py-4 flex items-center gap-4">
      {/* Icon */}
      <div className="shrink-0 w-10 h-10 rounded-lg bg-orange-100 flex items-center justify-center">
        <i className={`${group.icon} text-[18px] text-orange-500 leading-none`} />
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-gray-800">{group.heading}</p>
        <p className="text-xs text-gray-500 mt-0.5">
          {group.itemCount} {group.itemCount === 1 ? 'tool' : 'tools'}
          {group.liveCount > 0 && (
            <span className="ml-1 text-emerald-600">· {group.liveCount} live</span>
          )}
          <span className="mx-1.5 text-gray-300">·</span>
          {group.itemCount - group.liveCount > 0 && (
            <span>{group.itemCount - group.liveCount} in progress</span>
          )}
        </p>
      </div>

      {/* Pointer to sidebar */}
      <div className="shrink-0 flex items-center gap-1.5 text-xs text-orange-500 font-medium">
        <i className="ri-arrow-left-s-line text-[15px]" />
        <span className="hidden sm:inline">Navigate from the sidebar</span>
      </div>
    </div>
  );
}

// ── Inner component (requires Suspense) ─────────────────────────────────────

function NavHubInner() {
  const router       = useRouter();
  const searchParams = useSearchParams();
  const selectedSlug = searchParams.get('group');
  const groups       = getNavGroupModels();
  const selectedGroup = selectedSlug
    ? groups.find(g => g.slug === selectedSlug)
    : undefined;

  function handleSelect(slug: string) {
    if (slug === selectedSlug) {
      router.push('/');
    } else {
      router.push(`/?group=${slug}`);
    }
  }

  return (
    <div>
      {/* Section heading */}
      <div className="flex items-center justify-between mb-3">
        <h2 className="text-xs font-semibold uppercase tracking-widest text-gray-400">
          Navigation
        </h2>
        {selectedGroup && (
          <button
            onClick={() => router.push('/')}
            className="text-xs text-gray-400 hover:text-gray-600 flex items-center gap-1 transition-colors"
          >
            <i className="ri-close-line text-[13px]" />
            Clear
          </button>
        )}
      </div>

      {/* Group cards grid */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
        {groups.map(g => (
          <GroupCard
            key={g.slug}
            group={g}
            isSelected={g.slug === selectedSlug}
            onSelect={() => handleSelect(g.slug)}
          />
        ))}
      </div>

      {/* Selected group summary — compact; sidebar owns the full menu list */}
      {selectedGroup ? (
        <GroupSummaryPanel group={selectedGroup} />
      ) : (
        <div className="mt-5 rounded-xl border border-dashed border-gray-200 bg-white py-10 flex flex-col items-center justify-center text-center">
          <div className="w-10 h-10 rounded-full bg-gray-100 flex items-center justify-center mb-3">
            <i className="ri-layout-grid-line text-[18px] text-gray-400" />
          </div>
          <p className="text-sm font-medium text-gray-500">Select a category above</p>
          <p className="text-xs text-gray-400 mt-1">Tools will appear in the left sidebar</p>
        </div>
      )}
    </div>
  );
}

// ── Public export (with Suspense boundary) ───────────────────────────────────

export function NavigationGroupGrid() {
  return (
    <Suspense
      fallback={
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3 animate-pulse">
          {Array.from({ length: 12 }).map((_, i) => (
            <div key={i} className="h-20 rounded-xl bg-gray-100 border border-gray-200" />
          ))}
        </div>
      }
    >
      <NavHubInner />
    </Suspense>
  );
}
