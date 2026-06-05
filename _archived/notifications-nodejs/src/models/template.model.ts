import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { NotificationChannel, ProductType, TemplateScope, EditorType } from "../types";

export type TemplateStatus = "active" | "inactive";

interface TemplateAttributes {
  id: string;
  tenantId: string | null;
  templateKey: string;
  channel: NotificationChannel;
  name: string;
  description: string | null;
  status: TemplateStatus;
  isSystemTemplate: boolean;
  productType: ProductType | null;
  templateScope: TemplateScope;
  editorType: EditorType;
  category: string | null;
  isBrandable: boolean;
  createdAt?: Date;
  updatedAt?: Date;
}

interface TemplateCreationAttributes
  extends Optional<
    TemplateAttributes,
    | "id"
    | "tenantId"
    | "description"
    | "status"
    | "isSystemTemplate"
    | "productType"
    | "templateScope"
    | "editorType"
    | "category"
    | "isBrandable"
  > {}

export class Template extends Model<TemplateAttributes, TemplateCreationAttributes> {
  declare id: string;
  declare tenantId: string | null;
  declare templateKey: string;
  declare channel: NotificationChannel;
  declare name: string;
  declare description: string | null;
  declare status: TemplateStatus;
  declare isSystemTemplate: boolean;
  declare productType: ProductType | null;
  declare templateScope: TemplateScope;
  declare editorType: EditorType;
  declare category: string | null;
  declare isBrandable: boolean;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initTemplateModel(sequelize: Sequelize): void {
  Template.init(
    {
      id: {
        type: DataTypes.UUID,
        defaultValue: DataTypes.UUIDV4,
        primaryKey: true,
      },
      tenantId: {
        type: DataTypes.UUID,
        allowNull: true,
        defaultValue: null,
        field: "tenant_id",
      },
      templateKey: {
        type: DataTypes.STRING(200),
        allowNull: false,
        field: "template_key",
      },
      channel: {
        type: DataTypes.ENUM("email", "sms", "push", "in-app"),
        allowNull: false,
      },
      name: {
        type: DataTypes.STRING(200),
        allowNull: false,
      },
      description: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
      },
      status: {
        type: DataTypes.ENUM("active", "inactive"),
        allowNull: false,
        defaultValue: "active",
      },
      isSystemTemplate: {
        type: DataTypes.BOOLEAN,
        allowNull: false,
        defaultValue: false,
        field: "is_system_template",
      },
      productType: {
        type: DataTypes.STRING(50),
        allowNull: true,
        defaultValue: null,
        field: "product_type",
      },
      templateScope: {
        type: DataTypes.STRING(20),
        allowNull: false,
        defaultValue: "global",
        field: "template_scope",
      },
      editorType: {
        type: DataTypes.STRING(20),
        allowNull: false,
        defaultValue: "html",
        field: "editor_type",
      },
      category: {
        type: DataTypes.STRING(100),
        allowNull: true,
        defaultValue: null,
      },
      isBrandable: {
        type: DataTypes.BOOLEAN,
        allowNull: false,
        defaultValue: false,
        field: "is_brandable",
      },
    },
    {
      sequelize,
      tableName: "templates",
      timestamps: true,
      underscored: true,
      indexes: [
        {
          unique: true,
          fields: ["tenant_id", "template_key", "channel"],
          name: "uq_templates_tenant_key_channel",
        },
        {
          unique: true,
          fields: ["product_type", "channel", "template_key", "template_scope"],
          name: "uq_templates_product_channel_key_scope",
        },
        {
          fields: ["product_type"],
          name: "idx_templates_product_type",
        },
        {
          fields: ["template_scope"],
          name: "idx_templates_template_scope",
        },
      ],
    }
  );
}
