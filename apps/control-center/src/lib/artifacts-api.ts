import { cookies } from 'next/headers';

const ARTIFACTS_API_BASE = process.env.ARTIFACTS_API_BASE ?? 'http://127.0.0.1:5020';

export type ArtifactFeedbackLink = {
  linkId: number;
  feedbackActionId: number;
  feedbackActionTitle: string;
  feedbackActionStatus: 'OPEN' | 'IN_PROGRESS' | 'RESOLVED' | 'DISMISSED';
  feedbackId: number;
  inquiryType: 'BUG' | 'FEATURE_REQUEST' | 'SUGGESTION' | 'QUESTION' | 'GENERAL';
  summary: string;
  createdAt: string;
};

export type ArtifactFeedbackLinkView = {
  artifactType: 'FEATURE' | 'DEFECT' | 'REQUIREMENT' | 'MITIGATION';
  artifactId: number;
  links: ArtifactFeedbackLink[];
};

export class ArtifactsApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = 'ArtifactsApiError';
  }
}

async function artifactsRequest<T>(path: string): Promise<T> {
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;

  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${ARTIFACTS_API_BASE}${path}`, {
    method: 'GET',
    headers,
    cache: 'no-store',
  });

  if (!res.ok) {
    let msg = `HTTP ${res.status}`;
    try {
      const body = await res.json();
      msg = body.error ?? body.message ?? msg;
    } catch {}
    throw new ArtifactsApiError(res.status, msg);
  }

  return res.json() as Promise<T>;
}

export const artifactsApi = {
  getArtifactFeedbackLinks(
    artifactType: string,
    artifactId: number,
  ): Promise<ArtifactFeedbackLinkView> {
    return artifactsRequest<ArtifactFeedbackLinkView>(
      `/api/admin/artifacts/${encodeURIComponent(artifactType)}/${artifactId}/feedback-links`,
    );
  },
};
