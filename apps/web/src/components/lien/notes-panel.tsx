'use client';

import { useState } from 'react';
import { formatDateTime } from '@/lib/lien-utils';

interface Note {
  id: string;
  text: string;
  author: string;
  timestamp: string;
}

interface NotesPanelProps {
  notes: Note[];
  onAddNote: (text: string) => void;
  readOnly?: boolean;
}

export function NotesPanel({ notes, onAddNote, readOnly = false }: NotesPanelProps) {
  const [text, setText] = useState('');

  const handleSubmit = () => {
    if (!text.trim()) return;
    onAddNote(text.trim());
    setText('');
  };

  return (
    <div className="bg-white border border-gray-200 rounded-xl">
      <div className="px-5 py-4 border-b border-gray-100">
        <h3 className="text-sm font-semibold text-gray-800 flex items-center gap-2">
          <i className="ri-sticky-note-line text-base text-gray-400" />
          Notes ({notes.length})
        </h3>
      </div>
      {!readOnly && (
        <div className="px-5 py-3 border-b border-gray-100">
          <textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="Add a note..."
            rows={2}
            className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none"
          />
          <div className="flex justify-end mt-2">
            <button onClick={handleSubmit} disabled={!text.trim()} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed">
              Add Note
            </button>
          </div>
        </div>
      )}
      <div className="divide-y divide-gray-50 max-h-80 overflow-y-auto">
        {notes.length === 0 && <div className="px-5 py-6 text-center text-sm text-gray-400">No notes yet.</div>}
        {notes.map((note) => (
          <div key={note.id} className="px-5 py-3">
            <p className="text-sm text-gray-700">{note.text}</p>
            <p className="text-xs text-gray-400 mt-1">{note.author} &middot; {formatDateTime(note.timestamp)}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
