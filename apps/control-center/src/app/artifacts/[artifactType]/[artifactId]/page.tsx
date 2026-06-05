import Link from 'next/link';
import { notFound } from 'next/navigation';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { artifactsApi, ArtifactsApiError } from '@/lib/artifacts-api';
import { LinkedFeedbackPanel } from '@/components/artifacts/linked-feedback-panel';

export const dynamic = 'force-dynamic';

const VALID_TYPES = ['FEATURE', 'DEFECT', 'REQUIREMENT', 'MITIGATION'];
const TYPE_LABELS: Record<string, string> = {
  FEATURE: 'Feature',
  DEFECT: 'Defect',
  REQUIREMENT: 'Requirement',
  MITIGATION: 'Mitigation',
};

interface PageProps {
  params: Promise<{ artifactType: string; artifactId: string }>;
}

export default async function ArtifactDetailPage({ params }: PageProps) {
  await requirePlatformAdmin();

  const { artifactType, artifactId: rawId } = await params;
  const upperType = artifactType.toUpperCase();

  if (!VALID_TYPES.includes(upperType)) {
    notFound();
  }

  const artifactId = parseInt(rawId, 10);
  if (isNaN(artifactId) || artifactId <= 0) {
    notFound();
  }

  let data = null;
  let error: string | null = null;

  try {
    data = await artifactsApi.getArtifactFeedbackLinks(upperType, artifactId);
  } catch (err) {
    if (err instanceof ArtifactsApiError) {
      if (err.status === 404) {
        notFound();
      }
      error = err.message;
    } else {
      error = 'Unable to load feedback links. The artifacts service may be unavailable.';
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-2 text-sm text-gray-400">
        <Link href="/artifacts" className="hover:text-indigo-600">Artifacts</Link>
        <span>/</span>
        <Link href={`/artifacts/${upperType}`} className="hover:text-indigo-600">{TYPE_LABELS[upperType] ?? upperType}s</Link>
        <span>/</span>
        <span className="text-gray-600">#{artifactId}</span>
      </div>

      <div>
        <h1 className="text-2xl font-bold text-gray-900">
          {TYPE_LABELS[upperType] ?? upperType} #{artifactId}
        </h1>
        <p className="text-sm text-gray-500 mt-1">
          Reverse traceability view — linked feedback and actions for this artifact.
        </p>
      </div>

      <LinkedFeedbackPanel data={data} error={error} />
    </div>
  );
}
