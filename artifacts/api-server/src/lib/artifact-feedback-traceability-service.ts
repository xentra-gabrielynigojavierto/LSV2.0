import { FeedbackActionLink } from '../models/feedback-action-link.model';
import { FeedbackActionItem, type FeedbackActionStatus } from '../models/feedback-action-item.model';
import { FeedbackRecord, type InquiryType } from '../models/feedback-record.model';
import { Artifact } from '../models/artifact.model';

export const VALID_ARTIFACT_TYPES = ['FEATURE', 'DEFECT', 'REQUIREMENT', 'MITIGATION'] as const;
export type ValidArtifactType = (typeof VALID_ARTIFACT_TYPES)[number];

export interface ArtifactFeedbackLink {
  linkId: number;
  feedbackActionId: number;
  feedbackActionTitle: string;
  feedbackActionStatus: FeedbackActionStatus;
  feedbackId: number;
  inquiryType: InquiryType;
  summary: string;
  createdAt: string;
}

export interface ArtifactFeedbackLinkView {
  artifactType: ValidArtifactType;
  artifactId: number;
  links: ArtifactFeedbackLink[];
}

const STATUS_PRIORITY: Record<FeedbackActionStatus, number> = {
  OPEN: 0,
  IN_PROGRESS: 1,
  RESOLVED: 2,
  DISMISSED: 3,
};

function orderArtifactLinks(links: ArtifactFeedbackLink[]): ArtifactFeedbackLink[] {
  return [...links].sort((a, b) => {
    const statusDiff = STATUS_PRIORITY[a.feedbackActionStatus] - STATUS_PRIORITY[b.feedbackActionStatus];
    if (statusDiff !== 0) return statusDiff;

    const dateA = new Date(a.createdAt).getTime();
    const dateB = new Date(b.createdAt).getTime();
    if (dateA !== dateB) return dateB - dateA;

    return a.feedbackId - b.feedbackId;
  });
}

export function isValidArtifactType(value: string): value is ValidArtifactType {
  return (VALID_ARTIFACT_TYPES as readonly string[]).includes(value);
}

export class ArtifactFeedbackTraceabilityService {
  async assertArtifactExists(artifactType: ValidArtifactType, artifactId: number): Promise<boolean> {
    const artifact = await Artifact.findOne({
      where: { artifactType, id: artifactId },
    });
    return artifact !== null;
  }

  async getLinksForArtifact(
    _userId: string,
    artifactType: ValidArtifactType,
    artifactId: number,
  ): Promise<ArtifactFeedbackLinkView> {
    const exists = await this.assertArtifactExists(artifactType, artifactId);
    if (!exists) {
      const error = new Error(`Artifact not found: ${artifactType}/${artifactId}`);
      (error as any).statusCode = 404;
      throw error;
    }

    const linkRows = await FeedbackActionLink.findAll({
      where: { artifactType, artifactId },
      include: [
        {
          model: FeedbackActionItem,
          as: 'action',
          required: true,
          include: [
            {
              model: FeedbackRecord,
              as: 'feedback',
              required: true,
            },
          ],
        },
      ],
    });

    const links: ArtifactFeedbackLink[] = linkRows.map((row) => {
      const action = (row as any).action as FeedbackActionItem;
      const feedback = (action as any).feedback as FeedbackRecord;

      return {
        linkId: row.id,
        feedbackActionId: action.id,
        feedbackActionTitle: action.title,
        feedbackActionStatus: action.status,
        feedbackId: feedback.id,
        inquiryType: feedback.inquiryType,
        summary: feedback.summary,
        createdAt: feedback.createdAt.toISOString(),
      };
    });

    return {
      artifactType,
      artifactId,
      links: orderArtifactLinks(links),
    };
  }
}

export const artifactFeedbackTraceabilityService = new ArtifactFeedbackTraceabilityService();
