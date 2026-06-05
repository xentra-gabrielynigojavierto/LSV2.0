import { DataTypes, Model, Sequelize, Optional } from "sequelize";
import { RateLimitPolicyStatus } from "../types";

interface TenantRateLimitPolicyAttributes {
  id: string;
  tenantId: string;
  channel: string | null;
  maxRequestsPerMinute: number | null;
  maxAttemptsPerMinute: number | null;
  maxDailyUsage: number | null;
  maxMonthlyUsage: number | null;
  status: RateLimitPolicyStatus;
  createdAt?: Date;
  updatedAt?: Date;
}

interface TenantRateLimitPolicyCreationAttributes
  extends Optional<
    TenantRateLimitPolicyAttributes,
    "id" | "channel" | "maxRequestsPerMinute" | "maxAttemptsPerMinute" | "maxDailyUsage" | "maxMonthlyUsage"
  > {}

export class TenantRateLimitPolicy extends Model<
  TenantRateLimitPolicyAttributes,
  TenantRateLimitPolicyCreationAttributes
> {
  declare id: string;
  declare tenantId: string;
  declare channel: string | null;
  declare maxRequestsPerMinute: number | null;
  declare maxAttemptsPerMinute: number | null;
  declare maxDailyUsage: number | null;
  declare maxMonthlyUsage: number | null;
  declare status: RateLimitPolicyStatus;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initTenantRateLimitPolicyModel(sequelize: Sequelize): void {
  TenantRateLimitPolicy.init(
    {
      id: { type: DataTypes.UUID, defaultValue: DataTypes.UUIDV4, primaryKey: true },
      tenantId: { type: DataTypes.UUID, allowNull: false, field: "tenant_id" },
      channel: { type: DataTypes.STRING(50), allowNull: true, defaultValue: null },
      maxRequestsPerMinute: { type: DataTypes.INTEGER, allowNull: true, defaultValue: null, field: "max_requests_per_minute" },
      maxAttemptsPerMinute: { type: DataTypes.INTEGER, allowNull: true, defaultValue: null, field: "max_attempts_per_minute" },
      maxDailyUsage: { type: DataTypes.INTEGER, allowNull: true, defaultValue: null, field: "max_daily_usage" },
      maxMonthlyUsage: { type: DataTypes.INTEGER, allowNull: true, defaultValue: null, field: "max_monthly_usage" },
      status: {
        type: DataTypes.ENUM("active", "inactive"),
        allowNull: false,
        defaultValue: "active",
      },
    },
    {
      sequelize,
      tableName: "tenant_rate_limit_policies",
      timestamps: true,
      underscored: true,
      indexes: [
        { fields: ["tenant_id"] },
        { fields: ["tenant_id", "status"] },
        { fields: ["tenant_id", "channel"] },
      ],
    }
  );
}
