import { Sequelize } from 'sequelize';
import { initFeedbackRecordModel, FeedbackRecord } from './feedback-record.model';
import { initFeedbackActionItemModel, FeedbackActionItem } from './feedback-action-item.model';
import { initFeedbackActionLinkModel, FeedbackActionLink } from './feedback-action-link.model';
import { initArtifactModel, Artifact } from './artifact.model';

let sequelize: Sequelize | null = null;

export async function initDatabase(connectionUrl: string): Promise<Sequelize> {
  sequelize = new Sequelize(connectionUrl, {
    dialect: 'postgres',
    logging: process.env.NODE_ENV === 'development' ? console.log : false,
  });

  await sequelize.authenticate();
  console.log('[artifacts] Database connection established');

  initFeedbackRecordModel(sequelize);
  initFeedbackActionItemModel(sequelize);
  initFeedbackActionLinkModel(sequelize);
  initArtifactModel(sequelize);

  FeedbackActionItem.belongsTo(FeedbackRecord, { foreignKey: 'feedbackId', as: 'feedback' });
  FeedbackRecord.hasMany(FeedbackActionItem, { foreignKey: 'feedbackId', as: 'actions' });

  FeedbackActionLink.belongsTo(FeedbackActionItem, { foreignKey: 'feedbackActionId', as: 'action' });
  FeedbackActionItem.hasMany(FeedbackActionLink, { foreignKey: 'feedbackActionId', as: 'links' });

  const isDev = process.env.NODE_ENV !== 'production';
  await sequelize.sync({ alter: isDev });
  console.log('[artifacts] Models synchronized');

  return sequelize;
}

export function getSequelize(): Sequelize {
  if (!sequelize) throw new Error('Database has not been initialized');
  return sequelize;
}

export { FeedbackRecord } from './feedback-record.model';
export { FeedbackActionItem } from './feedback-action-item.model';
export { FeedbackActionLink } from './feedback-action-link.model';
export { Artifact } from './artifact.model';
