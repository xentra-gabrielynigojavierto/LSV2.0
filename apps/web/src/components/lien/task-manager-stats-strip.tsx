'use client';

export interface TaskStat {
  label: string;
  value: number;
  icon: string;
  color: string;
}

interface TaskManagerStatsStripProps {
  stats: TaskStat[];
}

export function TaskManagerStatsStrip({ stats }: TaskManagerStatsStripProps) {
  return (
    <div className="flex items-center gap-2 flex-wrap">
      {stats.map((stat) => (
        <div
          key={stat.label}
          className="flex items-center gap-1.5 bg-gray-50 border border-gray-100 rounded-lg px-2.5 py-1.5 shrink-0"
        >
          <i className={`${stat.icon} text-[11px] ${stat.color}`} />
          <span className="text-[11px] text-gray-500">{stat.label}</span>
          <span className={`text-[11px] font-semibold tabular-nums ${stat.color}`}>{stat.value}</span>
        </div>
      ))}
    </div>
  );
}
