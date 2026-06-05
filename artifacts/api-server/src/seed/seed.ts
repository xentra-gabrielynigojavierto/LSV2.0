import { initDatabase, FeedbackRecord, FeedbackActionItem, FeedbackActionLink } from '../models';
import { Artifact } from '../models/artifact.model';

async function seed(): Promise<void> {
  const dbUrl = process.env.DATABASE_URL || 'postgresql://postgres:password@localhost/heliumdb?sslmode=disable';
  await initDatabase(dbUrl);

  const existingArtifacts = await Artifact.count();
  if (existingArtifacts > 0) {
    console.log('[seed] Data already exists, skipping seed.');
    process.exit(0);
  }

  const artifacts = await Artifact.bulkCreate([
    { artifactType: 'FEATURE', title: 'Project Dashboard Performance Optimization', description: 'Improve load times for the main project dashboard' },
    { artifactType: 'FEATURE', title: 'Notification Channel Preferences', description: 'Allow users to configure preferred notification channels' },
    { artifactType: 'DEFECT', title: 'Login Timeout on Mobile Browsers', description: 'Session expires prematurely on iOS Safari' },
    { artifactType: 'DEFECT', title: 'PDF Export Missing Footer', description: 'Generated PDF reports have no footer content' },
    { artifactType: 'REQUIREMENT', title: 'HIPAA Audit Trail Compliance', description: 'All PHI access must be logged with timestamp and user ID' },
    { artifactType: 'REQUIREMENT', title: 'Multi-factor Authentication Support', description: 'Platform must support TOTP and WebAuthn for MFA' },
    { artifactType: 'MITIGATION', title: 'Rate Limiting for API Endpoints', description: 'Prevent abuse through per-tenant rate limits' },
    { artifactType: 'MITIGATION', title: 'Data Encryption at Rest', description: 'All PII must be encrypted using AES-256 at rest' },
  ]);

  const feedbackRecords = await FeedbackRecord.bulkCreate([
    { inquiryType: 'BUG', summary: 'The dashboard takes too long to load', conversation: 'User reports 10+ second load times on the main dashboard with 50+ projects.', rating: 2, goalAchievement: 'PARTIAL', confidenceLevel: 'HIGH', experienceQuality: 'OKAY' },
    { inquiryType: 'FEATURE_REQUEST', summary: 'Want to disable email notifications and use Slack only', conversation: 'Customer prefers Slack for all team notifications instead of email.', rating: 3, goalAchievement: 'NO', confidenceLevel: 'MEDIUM', experienceQuality: 'HUMAN' },
    { inquiryType: 'BUG', summary: 'Cannot stay logged in on my iPhone', conversation: 'Session keeps expiring after 5 minutes on Safari iOS 17.', rating: 1, goalAchievement: 'NO', confidenceLevel: 'HIGH', experienceQuality: 'ROBOTIC' },
    { inquiryType: 'SUGGESTION', summary: 'Add compliance reports to exports', conversation: 'Would be helpful to have HIPAA-specific audit reports available for download.', rating: 4, goalAchievement: 'PARTIAL', confidenceLevel: 'MEDIUM', experienceQuality: 'HUMAN' },
    { inquiryType: 'GENERAL', summary: 'Love the platform but concerned about security', conversation: 'Generally positive feedback with questions about encryption and data protection.', rating: 4, goalAchievement: 'YES', confidenceLevel: 'HIGH', experienceQuality: 'HUMAN' },
    { inquiryType: 'BUG', summary: 'PDF reports are missing the company footer', conversation: 'When exporting project reports to PDF, the footer with company branding is not included.', rating: 2, goalAchievement: 'NO', confidenceLevel: 'HIGH', experienceQuality: 'OKAY' },
    { inquiryType: 'FEATURE_REQUEST', summary: 'Need rate limiting controls for our API usage', conversation: 'Tenant wants visibility into their API rate limits and ability to see usage stats.', rating: 3, goalAchievement: 'PARTIAL', confidenceLevel: 'MEDIUM', experienceQuality: 'HUMAN' },
    { inquiryType: 'QUESTION', summary: 'Is MFA supported? We need it for compliance', conversation: 'Customer asking about MFA options before they can onboard their organization.', rating: 3, goalAchievement: 'NO', confidenceLevel: 'HIGH', experienceQuality: 'HUMAN' },
  ]);

  const actions = await FeedbackActionItem.bulkCreate([
    { feedbackId: feedbackRecords[0].id, title: 'Improve project dashboard performance', description: 'Optimize DB queries and add pagination for large project lists', status: 'IN_PROGRESS', priority: 'HIGH' },
    { feedbackId: feedbackRecords[1].id, title: 'Add notification channel preferences UI', description: 'Create settings page for users to choose notification channels', status: 'OPEN', priority: 'MEDIUM' },
    { feedbackId: feedbackRecords[2].id, title: 'Fix mobile session timeout issue', description: 'Investigate iOS Safari session handling and extend timeout', status: 'RESOLVED', priority: 'HIGH' },
    { feedbackId: feedbackRecords[3].id, title: 'Add HIPAA audit trail to exports', description: 'Include compliance audit data in export functionality', status: 'OPEN', priority: 'HIGH' },
    { feedbackId: feedbackRecords[4].id, title: 'Document encryption-at-rest practices', description: 'Publish documentation about data encryption strategy', status: 'IN_PROGRESS', priority: 'MEDIUM' },
    { feedbackId: feedbackRecords[5].id, title: 'Fix PDF export footer rendering', description: 'Ensure company branding footer appears in all PDF exports', status: 'OPEN', priority: 'MEDIUM' },
    { feedbackId: feedbackRecords[6].id, title: 'Expose rate limit dashboard to tenants', description: 'Build tenant-facing rate limit usage visibility', status: 'DISMISSED', priority: 'LOW' },
    { feedbackId: feedbackRecords[7].id, title: 'Implement MFA support (TOTP + WebAuthn)', description: 'Add multi-factor authentication options', status: 'IN_PROGRESS', priority: 'HIGH' },
    { feedbackId: feedbackRecords[0].id, title: 'Add caching layer for dashboard queries', description: 'Implement Redis caching for frequently accessed dashboard data', status: 'OPEN', priority: 'MEDIUM' },
  ]);

  await FeedbackActionLink.bulkCreate([
    { feedbackActionId: actions[0].id, artifactType: 'FEATURE', artifactId: artifacts[0].id },
    { feedbackActionId: actions[8].id, artifactType: 'FEATURE', artifactId: artifacts[0].id },
    { feedbackActionId: actions[1].id, artifactType: 'FEATURE', artifactId: artifacts[1].id },
    { feedbackActionId: actions[2].id, artifactType: 'DEFECT', artifactId: artifacts[2].id },
    { feedbackActionId: actions[5].id, artifactType: 'DEFECT', artifactId: artifacts[3].id },
    { feedbackActionId: actions[3].id, artifactType: 'REQUIREMENT', artifactId: artifacts[4].id },
    { feedbackActionId: actions[7].id, artifactType: 'REQUIREMENT', artifactId: artifacts[5].id },
    { feedbackActionId: actions[6].id, artifactType: 'MITIGATION', artifactId: artifacts[6].id },
    { feedbackActionId: actions[4].id, artifactType: 'MITIGATION', artifactId: artifacts[7].id },
  ]);

  console.log('[seed] Seed data created successfully');
  console.log(`  Artifacts: ${artifacts.length}`);
  console.log(`  Feedback records: ${feedbackRecords.length}`);
  console.log(`  Feedback actions: ${actions.length}`);
  console.log(`  Feedback action links: 9`);
  process.exit(0);
}

seed().catch((err) => {
  console.error('[seed] Error:', err);
  process.exit(1);
});
