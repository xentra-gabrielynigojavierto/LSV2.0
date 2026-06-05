'use client';

import type { TaskTemplateDto, TemplateContextType } from '@/lib/liens/lien-task-templates.types';

interface TemplatePickerProps {
  templates: TaskTemplateDto[];
  contextType: TemplateContextType;
  onSelect: (template: TaskTemplateDto) => void;
  onScratch: () => void;
  loading?: boolean;
}

const CONTEXT_LABELS: Record<TemplateContextType, string> = {
  GENERAL: 'General',
  CASE:    'Case',
  LIEN:    'Lien',
  STAGE:   'Stage',
};

const CONTEXT_COLORS: Record<TemplateContextType, string> = {
  GENERAL: 'bg-gray-100 text-gray-600',
  CASE:    'bg-blue-50 text-blue-600',
  LIEN:    'bg-indigo-50 text-indigo-600',
  STAGE:   'bg-purple-50 text-purple-600',
};

const PRIORITY_ICONS: Record<string, { icon: string; color: string }> = {
  LOW:    { icon: 'ri-arrow-down-line',    color: 'text-gray-400' },
  MEDIUM: { icon: 'ri-subtract-line',      color: 'text-blue-500' },
  HIGH:   { icon: 'ri-arrow-up-line',      color: 'text-orange-500' },
  URGENT: { icon: 'ri-alarm-warning-line', color: 'text-red-600' },
};

export function TemplatePicker({
  templates,
  contextType,
  onSelect,
  onScratch,
  loading = false,
}: TemplatePickerProps) {
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-600">
          Choose a template to pre-fill task details, or start from scratch.
        </p>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-8 text-gray-400">
          <i className="ri-loader-4-line animate-spin text-xl mr-2" />
          <span className="text-sm">Loading templates...</span>
        </div>
      ) : templates.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-200 py-6 text-center text-sm text-gray-400">
          <i className="ri-file-list-3-line text-2xl mb-1 block" />
          No templates available for this context.
        </div>
      ) : (
        <div className="space-y-2 max-h-64 overflow-y-auto pr-1">
          {templates.map((t) => {
            const pri = PRIORITY_ICONS[t.defaultPriority] ?? PRIORITY_ICONS.MEDIUM;
            return (
              <button
                key={t.id}
                type="button"
                onClick={() => onSelect(t)}
                className="w-full text-left rounded-lg border border-gray-200 px-4 py-3 hover:border-primary/40 hover:bg-primary/5 transition-colors group"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <span className="font-medium text-sm text-gray-800 truncate">{t.name}</span>
                      <span className={`text-xs px-1.5 py-0.5 rounded font-medium shrink-0 ${CONTEXT_COLORS[t.contextType] ?? CONTEXT_COLORS.GENERAL}`}>
                        {CONTEXT_LABELS[t.contextType] ?? t.contextType}
                      </span>
                    </div>
                    {t.description && (
                      <p className="text-xs text-gray-500 truncate">{t.description}</p>
                    )}
                    <div className="flex items-center gap-3 mt-1.5 text-xs text-gray-400">
                      <span className={`flex items-center gap-0.5 ${pri.color}`}>
                        <i className={pri.icon} />
                        {t.defaultPriority.charAt(0) + t.defaultPriority.slice(1).toLowerCase()}
                      </span>
                      {t.defaultDueOffsetDays != null && (
                        <span className="flex items-center gap-0.5">
                          <i className="ri-calendar-line" />
                          Due in {t.defaultDueOffsetDays}d
                        </span>
                      )}
                      {t.defaultRoleId && (
                        <span className="flex items-center gap-0.5">
                          <i className="ri-user-line" />
                          {t.defaultRoleId}
                        </span>
                      )}
                    </div>
                  </div>
                  <i className="ri-arrow-right-s-line text-gray-300 group-hover:text-primary mt-0.5 shrink-0" />
                </div>
              </button>
            );
          })}
        </div>
      )}

      <div className="pt-1 border-t border-gray-100">
        <button
          type="button"
          onClick={onScratch}
          className="w-full text-left rounded-lg border border-dashed border-gray-200 px-4 py-2.5 text-sm text-gray-500 hover:border-gray-400 hover:text-gray-700 transition-colors flex items-center gap-2"
        >
          <i className="ri-pencil-line" />
          Start from Scratch
        </button>
      </div>
    </div>
  );
}
