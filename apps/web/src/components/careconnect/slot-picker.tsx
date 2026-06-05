import type { AvailabilitySlot } from '@/types/careconnect';

interface SlotPickerProps {
  slot:       AvailabilitySlot;
  selected:   boolean;
  onSelect:   (slot: AvailabilitySlot) => void;
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('en-US', {
    hour:   'numeric',
    minute: '2-digit',
    hour12: true,
  });
}

export function SlotPicker({ slot, selected, onSelect }: SlotPickerProps) {
  const available = slot.isAvailable;

  return (
    <button
      type="button"
      disabled={!available}
      onClick={() => available && onSelect(slot)}
      className={[
        'w-full text-left rounded-lg border px-3 py-2.5 text-sm transition-all',
        selected
          ? 'border-primary bg-primary text-white shadow-sm'
          : available
            ? 'border-gray-200 bg-white hover:border-primary hover:bg-blue-50 text-gray-900'
            : 'border-gray-100 bg-gray-50 text-gray-300 cursor-not-allowed',
      ].join(' ')}
    >
      <span className="font-medium">
        {formatTime(slot.startUtc)} – {formatTime(slot.endUtc)}
      </span>
      {slot.serviceType && (
        <span className={`ml-2 text-xs ${selected ? 'text-blue-100' : 'text-gray-400'}`}>
          {slot.serviceType}
        </span>
      )}
      {!available && (
        <span className="ml-2 text-xs text-gray-300">Unavailable</span>
      )}
      {slot.location && (
        <div className={`mt-0.5 text-xs ${selected ? 'text-blue-100' : 'text-gray-400'}`}>
          {slot.location}
        </div>
      )}
    </button>
  );
}
