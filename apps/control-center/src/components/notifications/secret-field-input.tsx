'use client';

interface SecretFieldInputProps {
  id:           string;
  label:        string;
  name:         string;
  value:        string;
  onChange:     (v: string) => void;
  required?:    boolean;
  placeholder?: string;
  isConfigured?: boolean;
}

export function SecretFieldInput({
  id, label, name, value, onChange, required = false, placeholder, isConfigured = false,
}: SecretFieldInputProps) {
  return (
    <div>
      <label htmlFor={id} className="block text-xs font-medium text-gray-700 mb-1">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
        {isConfigured && !value && (
          <span className="ml-2 text-[10px] font-normal text-green-700 bg-green-50 border border-green-200 px-1.5 py-0.5 rounded-full">
            configured
          </span>
        )}
      </label>
      <input
        id={id}
        type="password"
        name={name}
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={isConfigured && !value ? 'Leave blank to keep existing' : placeholder ?? ''}
        required={required && !isConfigured}
        autoComplete="new-password"
        className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 shadow-sm placeholder:text-gray-400 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
      />
    </div>
  );
}
