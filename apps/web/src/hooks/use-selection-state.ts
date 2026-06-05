'use client';

import { useState, useCallback, useMemo } from 'react';

export interface SelectionState<T extends string = string> {
  selectedIds: Set<T>;
  isSelected: (id: T) => boolean;
  toggle: (id: T) => void;
  toggleAll: (allIds: T[]) => void;
  isAllSelected: (allIds: T[]) => boolean;
  clear: () => void;
  count: number;
  ids: T[];
}

export function useSelectionState<T extends string = string>(): SelectionState<T> {
  const [selectedIds, setSelectedIds] = useState<Set<T>>(new Set());

  const isSelected = useCallback((id: T) => selectedIds.has(id), [selectedIds]);

  const toggle = useCallback((id: T) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

  const toggleAll = useCallback((allIds: T[]) => {
    setSelectedIds((prev) => {
      const allSelected = allIds.length > 0 && allIds.every((id) => prev.has(id));
      if (allSelected) {
        return new Set();
      }
      return new Set(allIds);
    });
  }, []);

  const isAllSelected = useCallback(
    (allIds: T[]) => allIds.length > 0 && allIds.every((id) => selectedIds.has(id)),
    [selectedIds],
  );

  const clear = useCallback(() => setSelectedIds(new Set()), []);

  const count = selectedIds.size;
  const ids = useMemo(() => Array.from(selectedIds), [selectedIds]);

  return { selectedIds, isSelected, toggle, toggleAll, isAllSelected, clear, count, ids };
}
