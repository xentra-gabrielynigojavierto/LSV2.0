import { DataTypes, Model, Sequelize, Optional } from 'sequelize';

export type ReverseTraceArtifactType = 'FEATURE' | 'DEFECT' | 'REQUIREMENT' | 'MITIGATION';

export interface FeedbackActionLinkAttributes {
  id: number;
  feedbackActionId: number;
  artifactType: ReverseTraceArtifactType;
  artifactId: number;
  createdAt: Date;
  updatedAt: Date;
}

type FeedbackActionLinkCreation = Optional<FeedbackActionLinkAttributes, 'id' | 'createdAt' | 'updatedAt'>;

export class FeedbackActionLink extends Model<FeedbackActionLinkAttributes, FeedbackActionLinkCreation>
  implements FeedbackActionLinkAttributes {
  declare id: number;
  declare feedbackActionId: number;
  declare artifactType: ReverseTraceArtifactType;
  declare artifactId: number;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initFeedbackActionLinkModel(sequelize: Sequelize): void {
  FeedbackActionLink.init(
    {
      id: { type: DataTypes.INTEGER, autoIncrement: true, primaryKey: true },
      feedbackActionId: {
        type: DataTypes.INTEGER,
        allowNull: false,
        field: 'feedback_action_id',
        references: { model: 'feedback_action_items', key: 'id' },
      },
      artifactType: {
        type: DataTypes.ENUM('FEATURE', 'DEFECT', 'REQUIREMENT', 'MITIGATION'),
        allowNull: false,
        field: 'artifact_type',
      },
      artifactId: {
        type: DataTypes.INTEGER,
        allowNull: false,
        field: 'artifact_id',
      },
      createdAt: { type: DataTypes.DATE, allowNull: false, field: 'created_at' },
      updatedAt: { type: DataTypes.DATE, allowNull: false, field: 'updated_at' },
    },
    {
      sequelize,
      tableName: 'feedback_action_links',
      timestamps: true,
      underscored: true,
    },
  );
}
