"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { getTenantId, setTenantId } from "@/lib/api/client";
import { invalidateUnreadCount, refreshUnreadCount } from "@/lib/useUnreadCount";

interface Props {
  onTenantChange?: () => void;
}

export function TenantSwitcher({ onTenantChange }: Props) {
  const [current, setCurrent] = useState("default");
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setCurrent(getTenantId());
  }, []);

  useEffect(() => {
    if (editing) {
      inputRef.current?.focus();
      inputRef.current?.select();
    }
  }, [editing]);

  const startEditing = useCallback(() => {
    setDraft(current);
    setEditing(true);
  }, [current]);

  const apply = useCallback(() => {
    const trimmed = draft.trim();
    if (!trimmed) {
      setEditing(false);
      return;
    }
    setTenantId(trimmed);
    setCurrent(trimmed);
    setEditing(false);
    invalidateUnreadCount();
    refreshUnreadCount();
    onTenantChange?.();
  }, [draft, onTenantChange]);

  const cancel = useCallback(() => {
    setEditing(false);
  }, []);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === "Enter") apply();
      if (e.key === "Escape") cancel();
    },
    [apply, cancel]
  );

  return (
    <div className="flex items-center gap-1.5 text-xs">
      <svg
        className="h-3.5 w-3.5 text-gray-400"
        fill="none"
        stroke="currentColor"
        viewBox="0 0 24 24"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={2}
          d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
        />
      </svg>
      {editing ? (
        <div className="flex items-center gap-1">
          <input
            ref={inputRef}
            type="text"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={handleKeyDown}
            onBlur={apply}
            className="w-28 rounded border border-gray-300 bg-white px-1.5 py-0.5 text-xs text-gray-800 outline-none focus:border-blue-400 focus:ring-1 focus:ring-blue-200"
            placeholder="tenant-id"
          />
        </div>
      ) : (
        <button
          onClick={startEditing}
          className="flex items-center gap-1 rounded px-1.5 py-0.5 text-gray-500 hover:bg-gray-100 hover:text-gray-700 transition-colors"
          title="Switch tenant"
        >
          <span className="font-medium text-gray-700">{current}</span>
          <svg className="h-3 w-3 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
          </svg>
        </button>
      )}
    </div>
  );
}
