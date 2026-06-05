import type { AvailabilitySlot } from '@/types/careconnect';
import { SlotPicker } from './slot-picker';

interface AvailabilityListProps {
  slots:          AvailabilitySlot[];
  selectedSlotId: string | null;
  onSelectSlot:   (slot: AvailabilitySlot) => void;
}

/** Groups ISO-UTC slot starts by local calendar date (yyyy-MM-dd) */
function groupByDate(slots: AvailabilitySlot[]): Record<string, AvailabilitySlot[]> {
  const groups: Record<string, AvailabilitySlot[]> = {};
  for (const slot of slots) {
    const key = new Date(slot.startUtc).toLocaleDateString('en-CA');   // yyyy-MM-dd
    (groups[key] ??= []).push(slot);
  }
  return groups;
}

function formatDayHeading(dateKey: string): string {
  const d = new Date(`${dateKey}T12:00:00`);
  return d.toLocaleDateString('en-US', {
    weekday: 'long',
    month:   'long',
    day:     'numeric',
  });
}

export function AvailabilityList({
  slots,
  selectedSlotId,
  onSelectSlot,
}: AvailabilityListProps) {
  if (slots.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No availability found for the selected date range.</p>
        <p className="text-xs text-gray-300 mt-1">Try expanding the date range above.</p>
      </div>
    );
  }

  const groups    = groupByDate(slots);
  const dateKeys  = Object.keys(groups).sort();
  const available = slots.filter(s => s.isAvailable).length;

  return (
    <div className="space-y-5">
      {/* Summary */}
      <p className="text-xs text-gray-400">
        {available} available slot{available !== 1 ? 's' : ''} across {dateKeys.length} day{dateKeys.length !== 1 ? 's' : ''}
      </p>

      {dateKeys.map(dateKey => {
        const daySlots = groups[dateKey];
        const dayAvailable = daySlots.some(s => s.isAvailable);

        return (
          <section key={dateKey}>
            <h3 className={`text-xs font-semibold uppercase tracking-wider mb-2 ${
              dayAvailable ? 'text-gray-500' : 'text-gray-300'
            }`}>
              {formatDayHeading(dateKey)}
            </h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
              {daySlots.map(slot => (
                <SlotPicker
                  key={slot.id}
                  slot={slot}
                  selected={slot.id === selectedSlotId}
                  onSelect={onSelectSlot}
                />
              ))}
            </div>
          </section>
        );
      })}
    </div>
  );
}
