import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { BillingMode, BillingPlanStatus } from "../types";

interface TenantBillingPlanAttributes {
  id: string;
  tenantId: string;
  planName: string;
  status: BillingPlanStatus;
  billingMode: BillingMode;
  currency: string;
  effectiveFrom: Date;
  effectiveTo: Date | null;
  createdAt?: Date;
  updatedAt?: Date;
}

interface TenantBillingPlanCreationAttributes
  extends Optional<TenantBillingPlanAttributes, "id" | "effectiveTo"> {}

export class TenantBillingPlan extends Model<
  TenantBillingPlanAttributes,
  TenantBillingPlanCreationAttributes
> {
  declare id: string;
  declare tenantId: string;
  declare planName: string;
  declare status: BillingPlanStatus;
  declare billingMode: BillingMode;
  declare currency: string;
  declare effectiveFrom: Date;
  declare effectiveTo: Date | null;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initTenantBillingPlanModel(sequelize: Sequelize): void {
  TenantBillingPlan.init(
    {
      id: { type: DataTypes.UUID, defaultValue: DataTypes.UUIDV4, primaryKey: true },
      tenantId: { type: DataTypes.UUID, allowNull: false, field: "tenant_id" },
      planName: { type: DataTypes.STRING(200), allowNull: false, field: "plan_name" },
      status: {
        type: DataTypes.ENUM("active", "inactive", "archived"),
        allowNull: false,
        defaultValue: "active",
      },
      billingMode: {
        type: DataTypes.ENUM("usage_based", "flat_rate", "hybrid"),
        allowNull: false,
        defaultValue: "usage_based",
        field: "billing_mode",
      },
      currency: { type: DataTypes.STRING(10), allowNull: false, defaultValue: "USD" },
      effectiveFrom: { type: DataTypes.DATE, allowNull: false, field: "effective_from" },
      effectiveTo: { type: DataTypes.DATE, allowNull: true, defaultValue: null, field: "effective_to" },
    },
    {
      sequelize,
      tableName: "tenant_billing_plans",
      timestamps: true,
      underscored: true,
      indexes: [
        { fields: ["tenant_id"] },
        { fields: ["tenant_id", "status"] },
      ],
    }
  );
}
