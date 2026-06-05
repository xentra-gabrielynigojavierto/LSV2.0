import Link from 'next/link';
import { requirePlatformAdmin } from '@/lib/auth-guards';

export const dynamic = 'force-dynamic';

const ARTIFACT_TYPES = [
  { type: 'FEATURE', label: 'Features', description: 'Product features and enhancements', icon: '⚡' },
  { type: 'DEFECT', label: 'Defects', description: 'Bug reports and defect tracking', icon: '🐛' },
  { type: 'REQUIREMENT', label: 'Requirements', description: 'Compliance and business requirements', icon: '📋' },
  { type: 'MITIGATION', label: 'Mitigations', description: 'Risk mitigations and security controls', icon: '🛡️' },
];

export default async function ArtifactsPage() {
  await requirePlatformAdmin();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Artifacts</h1>
        <p className="text-sm text-gray-500 mt-1">View artifacts and their linked feedback traceability.</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {ARTIFACT_TYPES.map((at) => (
          <Link
            key={at.type}
            href={`/artifacts/${at.type}`}
            className="block bg-white rounded-xl border border-gray-200 p-6 hover:border-indigo-300 hover:shadow-sm transition-all"
          >
            <div className="flex items-center gap-3 mb-2">
              <span className="text-2xl">{at.icon}</span>
              <h2 className="text-lg font-semibold text-gray-800">{at.label}</h2>
            </div>
            <p className="text-sm text-gray-500">{at.description}</p>
          </Link>
        ))}
      </div>
    </div>
  );
}
