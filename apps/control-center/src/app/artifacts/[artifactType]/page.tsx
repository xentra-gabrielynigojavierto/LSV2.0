import Link from 'next/link';
import { notFound } from 'next/navigation';
import { requirePlatformAdmin } from '@/lib/auth-guards';

export const dynamic = 'force-dynamic';

const VALID_TYPES = ['FEATURE', 'DEFECT', 'REQUIREMENT', 'MITIGATION'];
const TYPE_LABELS: Record<string, string> = {
  FEATURE: 'Feature',
  DEFECT: 'Defect',
  REQUIREMENT: 'Requirement',
  MITIGATION: 'Mitigation',
};
const TYPE_DESCRIPTIONS: Record<string, string> = {
  FEATURE: 'Product features and enhancements linked to user feedback.',
  DEFECT: 'Bug reports and defect tracking linked to user feedback.',
  REQUIREMENT: 'Compliance and business requirements linked to user feedback.',
  MITIGATION: 'Risk mitigations and security controls linked to user feedback.',
};

interface PageProps {
  params: Promise<{ artifactType: string }>;
}

export default async function ArtifactTypePage({ params }: PageProps) {
  await requirePlatformAdmin();

  const { artifactType } = await params;
  const upperType = artifactType.toUpperCase();

  if (!VALID_TYPES.includes(upperType)) {
    notFound();
  }

  const label = TYPE_LABELS[upperType] ?? upperType;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-2 text-sm text-gray-400">
        <Link href="/artifacts" className="hover:text-indigo-600">Artifacts</Link>
        <span>/</span>
        <span className="text-gray-600">{label}s</span>
      </div>

      <div>
        <h1 className="text-2xl font-bold text-gray-900">{label}s</h1>
        <p className="text-sm text-gray-500 mt-1">{TYPE_DESCRIPTIONS[upperType]}</p>
      </div>

      <div className="bg-white rounded-xl border border-gray-200 p-6">
        <p className="text-sm text-gray-600 mb-4">
          To view linked feedback for a specific {label.toLowerCase()}, navigate directly to its detail page
          using the artifact ID.
        </p>
        <p className="text-sm text-gray-500">
          Example: <code className="bg-gray-100 px-2 py-0.5 rounded text-xs font-mono">/artifacts/{upperType}/1</code>
        </p>
      </div>
    </div>
  );
}
