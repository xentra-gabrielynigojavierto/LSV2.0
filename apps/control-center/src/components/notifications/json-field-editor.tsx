'use client';

interface JsonFieldEditorProps {
  id:          string;
  label:       string;
  value:       string;
  onChange:    (v: string) => void;
  required?:   boolean;
  placeholder?: string;
  rows?:       number;
}

export function JsonFieldEditor({
  id, label, value, onChange, required = false, placeholder, rows = 4,
}: JsonFieldEditorProps) {
  let parseError = '';
  if (value.trim()) {
    try { JSON.parse(value); } catch { parseError = 'Invalid JSON'; }
  }

  return (
    <div>
      <label htmlFor={id} className="block text-xs font-medium text-gray-700 mb-1">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
        <span className="ml-1 text-gray-400 font-normal">(JSON)</span>
      </label>
      <textarea
        id={id}
        value={value}
        onChange={e => onChange(e.target.value)}
        rows={rows}
        placeholder={placeholder ?? '{}'}
        required={required}
        spellCheck={false}
        className={`block w-full rounded-md border px-3 py-1.5 text-xs font-mono text-gray-900 shadow-sm placeholder:text-gray-400 focus:outline-none resize-y ${
          parseError ? 'border-red-400 focus:border-red-400 focus:ring-red-400' : 'border-gray-300 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500'
        }`}
      />
      {parseError && <p className="mt-0.5 text-[11px] text-red-600">{parseError}</p>}
    </div>
  );
}
