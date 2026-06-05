'use client';

import { useEffect, useState } from 'react';
import {
  useStartCaseWorkflow,
  useWorkflowDefinitions,
} from '@/hooks/use-case-workflows';
import { workflowApi, type ProductWorkflowRow, type WorkflowApiAdapter } from '@/lib/workflow';
import { WorkflowErrorState } from './workflow-error-state';

interface Props {
  caseId: string;
  productKey: string;
  buttonLabel?: string;
  /** Called after a workflow is successfully started, before refresh. */
  onStarted?: (row: ProductWorkflowRow) => void | Promise<void>;
  /** BFF adapter (defaults to SynqLien). */
  api?: WorkflowApiAdapter;
}

/**
 * E8.1 — inline expander form. Loads available definitions only when the
 * user opens the form so the panel stays cheap to mount. Validates that a
 * definition is chosen + a title is provided before posting.
 */
export function StartWorkflowAction({ caseId, productKey, buttonLabel, onStarted, api = workflowApi }: Props) {
  const [open, setOpen] = useState(false);
  const [defId, setDefId] = useState('');
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');

  const { definitions, loading: defsLoading, error: defsError, refresh: refreshDefs } =
    useWorkflowDefinitions(productKey, open, api);

  const { starting, error: startError, start, reset } = useStartCaseWorkflow(caseId, onStarted, api);

  // Auto-pick the first definition when the list loads.
  useEffect(() => {
    if (open && !defId && definitions.length > 0) {
      setDefId(definitions[0].id);
      if (!title) setTitle(definitions[0].name);
    }
  }, [open, defId, definitions, title]);

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-primary text-white hover:bg-primary/90 transition-colors"
      >
        <i className="ri-play-line text-sm" />
        {buttonLabel ?? 'Start workflow'}
      </button>
    );
  }

  const canSubmit = !starting && defId.length > 0 && title.trim().length > 0;

  return (
    <form
      className="space-y-2 border border-gray-200 rounded-md p-2.5 bg-gray-50"
      onSubmit={async (e) => {
        e.preventDefault();
        if (!canSubmit) return;
        const row = await start({
          workflowDefinitionId: defId,
          title: title.trim(),
          description: description.trim() ? description.trim() : undefined,
        });
        if (row) {
          setOpen(false);
          setDefId('');
          setTitle('');
          setDescription('');
        }
      }}
    >
      <div>
        <label className="block text-xs font-medium text-gray-600 mb-1">Workflow definition</label>
        {defsError ? (
          <WorkflowErrorState error={defsError} onRetry={() => void refreshDefs()} />
        ) : (
          <select
            value={defId}
            onChange={(e) => setDefId(e.target.value)}
            disabled={defsLoading || definitions.length === 0}
            className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 bg-white"
          >
            {defsLoading && <option value="">Loading…</option>}
            {!defsLoading && definitions.length === 0 && (
              <option value="">No definitions available</option>
            )}
            {definitions.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}{d.version ? ` (v${d.version})` : ''}
              </option>
            ))}
          </select>
        )}
      </div>

      <div>
        <label className="block text-xs font-medium text-gray-600 mb-1">Title</label>
        <input
          type="text"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Workflow title"
          maxLength={200}
          className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 bg-white"
        />
      </div>

      <div>
        <label className="block text-xs font-medium text-gray-600 mb-1">Description (optional)</label>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={2}
          maxLength={500}
          className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 bg-white resize-none"
        />
      </div>

      {startError && <WorkflowErrorState error={startError} onRetry={reset} />}

      <div className="flex items-center gap-2 pt-1">
        <button
          type="submit"
          disabled={!canSubmit}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-primary text-white hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {starting ? (<><i className="ri-loader-4-line animate-spin" />Starting…</>) : 'Start'}
        </button>
        <button
          type="button"
          onClick={() => { setOpen(false); reset(); }}
          className="px-3 py-1.5 text-xs font-medium rounded-md text-gray-600 hover:bg-gray-100 transition-colors"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
