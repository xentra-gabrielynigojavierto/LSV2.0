import { DataTypes, Model, Sequelize, Optional } from "sequelize";

export type TemplateVersionStatus = "draft" | "published" | "retired";

interface TemplateVersionAttributes {
  id: string;
  templateId: string;
  versionNumber: number;
  subjectTemplate: string | null;
  bodyTemplate: string;
  textTemplate: string | null;
  variablesSchemaJson: string | null;
  sampleDataJson: string | null;
  editorJson: string | null;
  designTokensJson: string | null;
  layoutType: string | null;
  status: TemplateVersionStatus;
  publishedAt: Date | null;
  createdAt?: Date;
  updatedAt?: Date;
}

interface TemplateVersionCreationAttributes
  extends Optional<
    TemplateVersionAttributes,
    | "id"
    | "versionNumber"
    | "subjectTemplate"
    | "textTemplate"
    | "variablesSchemaJson"
    | "sampleDataJson"
    | "editorJson"
    | "designTokensJson"
    | "layoutType"
    | "status"
    | "publishedAt"
  > {}

export class TemplateVersion extends Model<TemplateVersionAttributes, TemplateVersionCreationAttributes> {
  declare id: string;
  declare templateId: string;
  declare versionNumber: number;
  declare subjectTemplate: string | null;
  declare bodyTemplate: string;
  declare textTemplate: string | null;
  declare variablesSchemaJson: string | null;
  declare sampleDataJson: string | null;
  declare editorJson: string | null;
  declare designTokensJson: string | null;
  declare layoutType: string | null;
  declare status: TemplateVersionStatus;
  declare publishedAt: Date | null;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initTemplateVersionModel(sequelize: Sequelize): void {
  TemplateVersion.init(
    {
      id: {
        type: DataTypes.UUID,
        defaultValue: DataTypes.UUIDV4,
        primaryKey: true,
      },
      templateId: {
        type: DataTypes.UUID,
        allowNull: false,
        field: "template_id",
      },
      versionNumber: {
        type: DataTypes.INTEGER,
        allowNull: false,
        defaultValue: 1,
        field: "version_number",
      },
      subjectTemplate: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "subject_template",
      },
      bodyTemplate: {
        type: DataTypes.TEXT("long"),
        allowNull: false,
        field: "body_template",
      },
      textTemplate: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "text_template",
      },
      variablesSchemaJson: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "variables_schema_json",
      },
      sampleDataJson: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "sample_data_json",
      },
      editorJson: {
        type: DataTypes.TEXT("long"),
        allowNull: true,
        defaultValue: null,
        field: "editor_json",
      },
      designTokensJson: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "design_tokens_json",
      },
      layoutType: {
        type: DataTypes.STRING(50),
        allowNull: true,
        defaultValue: null,
        field: "layout_type",
      },
      status: {
        type: DataTypes.ENUM("draft", "published", "retired"),
        allowNull: false,
        defaultValue: "draft",
      },
      publishedAt: {
        type: DataTypes.DATE,
        allowNull: true,
        defaultValue: null,
        field: "published_at",
      },
    },
    {
      sequelize,
      tableName: "template_versions",
      timestamps: true,
      underscored: true,
    }
  );
}
