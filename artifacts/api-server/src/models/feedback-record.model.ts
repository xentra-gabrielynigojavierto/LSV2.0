import { DataTypes, Model, Sequelize, Optional } from 'sequelize';

export type InquiryType = 'BUG' | 'FEATURE_REQUEST' | 'SUGGESTION' | 'QUESTION' | 'GENERAL';

export interface FeedbackRecordAttributes {
  id: number;
  inquiryType: InquiryType;
  summary: string;
  conversation: string | null;
  rating: number | null;
  goalAchievement: string | null;
  confidenceLevel: string | null;
  experienceQuality: string | null;
  requestedCapability: string | null;
  workspaceId: string | null;
  createdAt: Date;
  updatedAt: Date;
}

type FeedbackRecordCreation = Optional<FeedbackRecordAttributes, 'id' | 'createdAt' | 'updatedAt'>;

export class FeedbackRecord extends Model<FeedbackRecordAttributes, FeedbackRecordCreation>
  implements FeedbackRecordAttributes {
  declare id: number;
  declare inquiryType: InquiryType;
  declare summary: string;
  declare conversation: string | null;
  declare rating: number | null;
  declare goalAchievement: string | null;
  declare confidenceLevel: string | null;
  declare experienceQuality: string | null;
  declare requestedCapability: string | null;
  declare workspaceId: string | null;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initFeedbackRecordModel(sequelize: Sequelize): void {
  FeedbackRecord.init(
    {
      id: { type: DataTypes.INTEGER, autoIncrement: true, primaryKey: true },
      inquiryType: {
        type: DataTypes.ENUM('BUG', 'FEATURE_REQUEST', 'SUGGESTION', 'QUESTION', 'GENERAL'),
        allowNull: false,
        field: 'inquiry_type',
      },
      summary: { type: DataTypes.TEXT, allowNull: false },
      conversation: { type: DataTypes.TEXT, allowNull: true },
      rating: { type: DataTypes.INTEGER, allowNull: true },
      goalAchievement: { type: DataTypes.STRING(20), allowNull: true, field: 'goal_achievement' },
      confidenceLevel: { type: DataTypes.STRING(20), allowNull: true, field: 'confidence_level' },
      experienceQuality: { type: DataTypes.STRING(20), allowNull: true, field: 'experience_quality' },
      requestedCapability: { type: DataTypes.TEXT, allowNull: true, field: 'requested_capability' },
      workspaceId: { type: DataTypes.STRING(100), allowNull: true, field: 'workspace_id' },
      createdAt: { type: DataTypes.DATE, allowNull: false, field: 'created_at' },
      updatedAt: { type: DataTypes.DATE, allowNull: false, field: 'updated_at' },
    },
    {
      sequelize,
      tableName: 'feedback_records',
      timestamps: true,
      underscored: true,
    },
  );
}
