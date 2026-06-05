'use client';

import type { RelatedEventsData, RelatedAuditEvent } from '@/types/control-center';
import { SeverityBadge, CategoryBadge, formatUtcFull } from './synqaudit-badges';

interface Props {
  data:          RelatedEventsData;
  anchorAuditId: string;
}

const MATCH_LABEL: Record<RelatedEventsData['related'][0]['matchedBy'], string> = {
  correlation_id:       'Correlation ID',
  session_id:           'Session ID',
  actor_entity_window:  'Actor + Entity (±4 h)',
  actor_window:         'Actor (±2 h)',
};

const MATCH_COLORS: Record<RelatedEventsData['related'][0]['matchedBy'], string> = {
  correlation_id:       'bg-indigo-50 text-indigo-700 border-indigo-200',
  session_id:           'bg-violet-50 text-violet-700 border-violet-200',
  actor_entity_window:  'bg-amber-50  text-amber-700  border-amber-200',
  actor_window:         'bg-gray-100  text-gray-600   border-gray-300',
};

/**
 * RelatedEventsTimeline — renders the correlation engine result.
 *
 * Groups events by matchedBy tier so investigators can quickly assess how
 * strongly correlated each batch of events is.
 */
export function RelatedEventsTimeline({ data, anchorAuditId }: Props) {

  if (data.totalRelated === 0) {
    return (
      <div className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center">
        <p className="text-sm font-medium text-gray-500">No related events found.</p>
        <p className="text-xs text-gray-400 mt-1">
          No correlation keys (correlationId, sessionId, actor+entity) matched other events.
        </p>
        <a
          href={`/synqaudit/investigation`}
          className="mt-4 inline-flex items-center gap-1 text-xs text-indigo-600 hover:text-indigo-800 font-medium"
        >
          Open investigation workspace
        </a>
      </div>
    );
  }

  return (
    <div className="space-y-4">

      {/* Summary strip */}
      <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 flex flex-wrap items-center gap-4 text-sm">
        <div>
          <span className="text-gray-500">Anchor</span>
          <span className="ml-2 font-mono text-xs text-gray-700 bg-gray-100 px-1.5 py-0.5 rounded">
            {data.anchorEventType}
          </span>
        </div>
        <div>
          <span className="text-gray-500">Strategy</span>
          <span className="ml-2 font-medium text-gray-800">
            {MATCH_LABEL[data.strategyUsed as keyof typeof MATCH_LABEL] ?? data.strategyUsed}
          </span>
        </div>
        <div className="ml-auto font-semibold text-gray-800">
          {data.totalRelated} related event{data.totalRelated !== 1 ? 's' : ''}
        </div>
      </div>

      {/* Timeline */}
      <div className="rounded-lg border border-gray-200 bg-white divide-y divide-gray-100">
        {data.related.map((r, idx) => (
          <RelatedEventRow key={r.event.id ?? idx} item={r} anchorAuditId={anchorAuditId} />
        ))}
      </div>
    </div>
  );
}

function RelatedEventRow({ item, anchorAuditId }: { item: RelatedAuditEvent; anchorAuditId: string }) {
  const { matchedBy, matchKey, event: e } = item;
  const colorClass = MATCH_COLORS[matchedBy];
  const matchLabel = MATCH_LABEL[matchedBy];

  return (
    <div className="flex items-start gap-4 px-4 py-3 hover:bg-gray-50 transition-colors">

      {/* Match badge */}
      <div className="shrink-0 pt-0.5">
        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold border ${colorClass} whitespace-nowrap`}>
          {matchLabel}
        </span>
      </div>

      {/* Event info */}
      <div className="flex-1 min-w-0 space-y-0.5">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-sm font-medium text-gray-900 truncate">{e.eventType}</span>
          <SeverityBadge value={e.severity} />
          <CategoryBadge value={e.category} />
        </div>
        <p className="text-xs text-gray-500 truncate">{e.description}</p>
        <div className="flex items-center gap-3 text-[11px] text-gray-400 flex-wrap">
          <span className="font-mono">{formatUtcFull(e.occurredAtUtc)}</span>
          {e.actorLabel && <span>Actor: {e.actorLabel}</span>}
          {e.source     && <span>Source: {e.source}</span>}
          <span className="font-mono truncate" title={matchKey}>key: {matchKey.length > 24 ? `${matchKey.slice(0, 24)}…` : matchKey}</span>
        </div>
      </div>

      {/* Navigation link */}
      <div className="shrink-0 flex flex-col gap-1 items-end">
        <a
          href={`/synqaudit/related/${encodeURIComponent(e.id)}`}
          className="text-[11px] text-indigo-600 hover:text-indigo-800 font-medium whitespace-nowrap"
          title="View this event's related chain"
        >
          Related chain →
        </a>
        {e.correlationId && (
          <a
            href={`/synqaudit/trace?correlationId=${encodeURIComponent(e.correlationId)}`}
            className="text-[11px] text-gray-400 hover:text-gray-700 whitespace-nowrap"
          >
            Trace ID
          </a>
        )}
      </div>
    </div>
  );
}
