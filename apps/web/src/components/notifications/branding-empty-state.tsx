'use client';

interface BrandingEmptyStateProps {
  onCreateClick: () => void;
}

export function BrandingEmptyState({ onCreateClick }: BrandingEmptyStateProps) {
  return (
    <div className="text-center py-16 px-6">
      <div className="mx-auto w-16 h-16 rounded-full bg-indigo-50 flex items-center justify-center mb-4">
        <i className="ri-palette-line text-3xl text-indigo-400" />
      </div>

      <h2 className="text-lg font-semibold text-gray-900 mb-2">
        No branding profiles yet
      </h2>
      <p className="text-sm text-gray-500 max-w-md mx-auto mb-2">
        Brand profiles control how your notification emails look &mdash; your logo,
        colors, fonts, and contact information are applied automatically to every
        outgoing message.
      </p>
      <p className="text-sm text-gray-500 max-w-md mx-auto mb-6">
        Create your first branding profile to ensure your notifications reflect
        your organisation&rsquo;s identity.
      </p>

      <button
        type="button"
        onClick={onCreateClick}
        className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 transition-colors"
      >
        <i className="ri-add-line text-base" />
        Create Your First Branding Profile
      </button>
    </div>
  );
}
