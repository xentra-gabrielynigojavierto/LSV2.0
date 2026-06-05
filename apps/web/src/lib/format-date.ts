const TZ = 'America/New_York';

export function formatTimestamp(iso: string): { date: string; time: string } {
  const d = new Date(iso);
  const date = d.toLocaleDateString('en-US', {
    month:    'short',
    day:      'numeric',
    year:     'numeric',
    timeZone: TZ,
  });
  const time = d.toLocaleTimeString('en-US', {
    hour:     'numeric',
    minute:   '2-digit',
    hour12:   true,
    timeZone: TZ,
  });
  return { date, time };
}

export function formatShortTimestamp(iso: string): string {
  const { date, time } = formatTimestamp(iso);
  return `${date}, ${time}`;
}
