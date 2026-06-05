import { DataTypes, Model, Sequelize, Optional } from 'sequelize';

export type FeedbackActionStatus = 'OPEN' | 'IN_PROGRESS' | 'RESOLVED' | 'DISMISSED';

export interface FeedbackActionItemAttributes {
  id: number;
  feedbackId: number;
  title: string;
  description: string | null;
  status: FeedbackActionStatus;
  priority: string | null;
  createdAt: Date;
  updatedAt: Date;
}

type FeedbackActionItemCreation = Optional<FeedbackActionItemAttributes, 'id' | 'createdAt' | 'updatedAt'>;

export class FeedbackActionItem extends Model<FeedbackActionItemAttributes, FeedbackActionItemCreation>
  implements FeedbackActionItemAttributes {
  declare id: number;
  declare feedbackId: number;
  declare title: string;
  declare description: string | null;
  declare status: FeedbackActionStatus;
  declare priority: string | null;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initFeedbackActionItemModel(sequelize: Sequelize): void {
  FeedbackActionItem.init(
    {
      id: { type: DataTypes.INTEGER, autoIncrement: true, primaryKey: true },
      feedbackId: {
        type: DataTypes.INTEGER,
        allowNull: false,
        field: 'feedback_id',
        references: { model: 'feedback_records', key: 'id' },
      },
      title: { type: DataTypes.STRING(500), allowNull: false },
      description: { type: DataTypes.TEXT, allowNull: true },
      status: {
        type: DataTypes.ENUM('OPEN', 'IN_PROGRESS', 'RESOLVED', 'DISMISSED'),
        allowNull: false,
        defaultValue: 'OPEN',
      },
      priority: { type: DataTypes.STRING(20), allowNull: true },
      createdAt: { type: DataTypes.DATE, allowNull: false, field: 'created_at' },
      updatedAt: { type: DataTypes.DATE, allowNull: false, field: 'updated_at' },
    },
    {
      sequelize,
      tableName: 'feedback_action_items',
      timestamps: true,
      underscored: true,
    },
  );
}
