import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell } from '@/components/shell/cc-shell';
import { TemplateEditorClient } from './template-editor-client';

export const dynamic = 'force-dynamic';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function TemplateEditorPage({ params }: Props) {
  const session = await requirePlatformAdmin();
  const { id } = await params;

  return (
    <CCShell userEmail={session.email}>
      <TemplateEditorClient templateId={id} />
    </CCShell>
  );
}
