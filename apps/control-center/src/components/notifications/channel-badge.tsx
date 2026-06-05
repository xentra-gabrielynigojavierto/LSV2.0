import type { NotifChannel } from '@/lib/notifications-api';

const MAP: Record<NotifChannel, { label: string; icon: string; cls: string }> = {
  'email':  { label: 'Email',  icon: 'ri-mail-line',            cls: 'bg-violet-50 text-violet-700 border-violet-200' },
  'sms':    { label: 'SMS',    icon: 'ri-message-3-line',       cls: 'bg-sky-50    text-sky-700    border-sky-200'    },
  'push':   { label: 'Push',   icon: 'ri-notification-3-line',  cls: 'bg-orange-50 text-orange-700 border-orange-200' },
  'in-app': { label: 'In-App', icon: 'ri-apps-line',            cls: 'bg-teal-50   text-teal-700   border-teal-200'  },
};

export function ChannelBadge({ channel }: { channel: string }) {
  const cfg = MAP[channel as NotifChannel] ?? { label: channel, icon: 'ri-question-line', cls: 'bg-gray-50 text-gray-600 border-gray-200' };
  return (
    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold border ${cfg.cls}`}>
      <i className={`${cfg.icon} text-[11px]`} />
      {cfg.label}
    </span>
  );
}
