import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { ProductType } from "../types";

interface TenantBrandingAttributes {
  id: string;
  tenantId: string;
  productType: ProductType;
  brandName: string;
  logoUrl: string | null;
  primaryColor: string | null;
  secondaryColor: string | null;
  accentColor: string | null;
  textColor: string | null;
  backgroundColor: string | null;
  buttonRadius: string | null;
  fontFamily: string | null;
  emailHeaderHtml: string | null;
  emailFooterHtml: string | null;
  supportEmail: string | null;
  supportPhone: string | null;
  websiteUrl: string | null;
  createdAt?: Date;
  updatedAt?: Date;
}

interface TenantBrandingCreationAttributes
  extends Optional<
    TenantBrandingAttributes,
    | "id"
    | "logoUrl"
    | "primaryColor"
    | "secondaryColor"
    | "accentColor"
    | "textColor"
    | "backgroundColor"
    | "buttonRadius"
    | "fontFamily"
    | "emailHeaderHtml"
    | "emailFooterHtml"
    | "supportEmail"
    | "supportPhone"
    | "websiteUrl"
  > {}

export class TenantBranding extends Model<TenantBrandingAttributes, TenantBrandingCreationAttributes> {
  declare id: string;
  declare tenantId: string;
  declare productType: ProductType;
  declare brandName: string;
  declare logoUrl: string | null;
  declare primaryColor: string | null;
  declare secondaryColor: string | null;
  declare accentColor: string | null;
  declare textColor: string | null;
  declare backgroundColor: string | null;
  declare buttonRadius: string | null;
  declare fontFamily: string | null;
  declare emailHeaderHtml: string | null;
  declare emailFooterHtml: string | null;
  declare supportEmail: string | null;
  declare supportPhone: string | null;
  declare websiteUrl: string | null;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initTenantBrandingModel(sequelize: Sequelize): void {
  TenantBranding.init(
    {
      id: {
        type: DataTypes.UUID,
        defaultValue: DataTypes.UUIDV4,
        primaryKey: true,
      },
      tenantId: {
        type: DataTypes.UUID,
        allowNull: false,
        field: "tenant_id",
      },
      productType: {
        type: DataTypes.STRING(50),
        allowNull: false,
        field: "product_type",
      },
      brandName: {
        type: DataTypes.STRING(200),
        allowNull: false,
        field: "brand_name",
      },
      logoUrl: {
        type: DataTypes.STRING(500),
        allowNull: true,
        defaultValue: null,
        field: "logo_url",
      },
      primaryColor: {
        type: DataTypes.STRING(20),
        allowNull: true,
        defaultValue: null,
        field: "primary_color",
      },
      secondaryColor: {
        type: DataTypes.STRING(20),
        allowNull: true,
        defaultValue: null,
        field: "secondary_color",
      },
      accentColor: {
        type: DataTypes.STRING(20),
        allowNull: true,
        defaultValue: null,
        field: "accent_color",
      },
      textColor: {
        type: DataTypes.STRING(20),
        allowNull: true,
        defaultValue: null,
        field: "text_color",
      },
      backgroundColor: {
        type: DataTypes.STRING(20),
        allowNull: true,
        defaultValue: null,
        field: "background_color",
      },
      buttonRadius: {
        type: DataTypes.STRING(20),
        allowNull: true,
        defaultValue: null,
        field: "button_radius",
      },
      fontFamily: {
        type: DataTypes.STRING(100),
        allowNull: true,
        defaultValue: null,
        field: "font_family",
      },
      emailHeaderHtml: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "email_header_html",
      },
      emailFooterHtml: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "email_footer_html",
      },
      supportEmail: {
        type: DataTypes.STRING(200),
        allowNull: true,
        defaultValue: null,
        field: "support_email",
      },
      supportPhone: {
        type: DataTypes.STRING(50),
        allowNull: true,
        defaultValue: null,
        field: "support_phone",
      },
      websiteUrl: {
        type: DataTypes.STRING(500),
        allowNull: true,
        defaultValue: null,
        field: "website_url",
      },
    },
    {
      sequelize,
      tableName: "tenant_branding",
      timestamps: true,
      underscored: true,
      indexes: [
        {
          unique: true,
          fields: ["tenant_id", "product_type"],
          name: "uq_tenant_branding_tenant_product",
        },
        {
          fields: ["tenant_id"],
          name: "idx_tenant_branding_tenant",
        },
        {
          fields: ["product_type"],
          name: "idx_tenant_branding_product",
        },
      ],
    }
  );
}
