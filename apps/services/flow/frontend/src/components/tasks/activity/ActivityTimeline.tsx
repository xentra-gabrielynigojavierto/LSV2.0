"use client";

import { useCallback, useEffect, useState } from "react";
import { ActivityItem } from "./ActivityItem";
import { getActivityEvents, subscribe } from "@/lib/activityStore";
import type { ActivityEvent } from "@/types/activity";

interface ActivityTimelineProps {
  taskId: string;
}

export function ActivityTimeline({ taskId }: ActivityTimelineProps) {
  const [events, setEvents] = useState<ActivityEvent[]>([]);

  const refresh = useCallback(() => {
    setEvents(getActivityEvents(taskId));
  }, [taskId]);

  useEffect(() => {
    refresh();
    const unsub = subscribe(refresh);
    return unsub;
  }, [refresh]);

  return (
    <section>
      <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-2">
        Activity
      </h3>
      {events.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 px-4 py-4 text-center">
          <p className="text-xs text-gray-400">No activity yet</p>
        </div>
      ) : (
        <div className="divide-y divide-gray-100 max-h-64 overflow-y-auto">
          {events.map((event) => (
            <ActivityItem key={event.id} event={event} />
          ))}
        </div>
      )}
    </section>
  );
}
