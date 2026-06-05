import { DataTypes, Model, Sequelize, Optional } from 'sequelize';
import type { ReverseTraceArtifactType } from './feedback-action-link.model';

export interface ArtifactAttributes {
  id: number;
  artifactType: ReverseTraceArtifactType;
  title: string;
  description: string | null;
  createdAt: Date;
  updatedAt: Date;
}

type ArtifactCreation = Optional<ArtifactAttributes, 'id' | 'createdAt' | 'updatedAt'>;

export class Artifact extends Model<ArtifactAttributes, ArtifactCreation>
  implements ArtifactAttributes {
  declare id: number;
  declare artifactType: ReverseTraceArtifactType;
  declare title: string;
  declare description: string | null;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initArtifactModel(sequelize: Sequelize): void {
  Artifact.init(
    {
      id: { type: DataTypes.INTEGER, autoIncrement: true, primaryKey: true },
      artifactType: {
        type: DataTypes.ENUM('FEATURE', 'DEFECT', 'REQUIREMENT', 'MITIGATION'),
        allowNull: false,
        field: 'artifact_type',
      },
      title: { type: DataTypes.STRING(500), allowNull: false },
      description: { type: DataTypes.TEXT, allowNull: true },
      createdAt: { type: DataTypes.DATE, allowNull: false, field: 'created_at' },
      updatedAt: { type: DataTypes.DATE, allowNull: false, field: 'updated_at' },
    },
    {
      sequelize,
      tableName: 'artifacts',
      timestamps: true,
      underscored: true,
    },
  );
}
