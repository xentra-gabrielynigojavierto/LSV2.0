import { DataTypes, Model, Sequelize, Optional } from "sequelize";

export type WebhookProcessingStatus = "received" | "processed" | "failed" | "skipped";

interface ProviderWebhookLogAttributes {
  id: string;
  tenantId: string | null;
  provider: string;
  channel: string | null;
  requestHeadersJson: string;
  payloadJson: string;
  signatureVerified: boolean;
  processingStatus: WebhookProcessingStatus;
  processingError: string | null;
  receivedAt: Date;
  createdAt?: Date;
  updatedAt?: Date;
}

interface ProviderWebhookLogCreationAttributes
  extends Optional<
    ProviderWebhookLogAttributes,
    "id" | "tenantId" | "channel" | "processingError"
  > {}

export class ProviderWebhookLog extends Model<
  ProviderWebhookLogAttributes,
  ProviderWebhookLogCreationAttributes
> {
  declare id: string;
  declare tenantId: string | null;
  declare provider: string;
  declare channel: string | null;
  declare requestHeadersJson: string;
  declare payloadJson: string;
  declare signatureVerified: boolean;
  declare processingStatus: WebhookProcessingStatus;
  declare processingError: string | null;
  declare receivedAt: Date;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initProviderWebhookLogModel(sequelize: Sequelize): void {
  ProviderWebhookLog.init(
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
      provider: {
        type: DataTypes.STRING(100),
        allowNull: false,
      },
      channel: {
        type: DataTypes.STRING(50),
        allowNull: true,
        defaultValue: null,
      },
      requestHeadersJson: {
        type: DataTypes.TEXT,
        allowNull: false,
        field: "request_headers_json",
      },
      payloadJson: {
        type: DataTypes.TEXT("long"),
        allowNull: false,
        field: "payload_json",
      },
      signatureVerified: {
        type: DataTypes.BOOLEAN,
        allowNull: false,
        defaultValue: false,
        field: "signature_verified",
      },
      processingStatus: {
        type: DataTypes.ENUM("received", "processed", "failed", "skipped"),
        allowNull: false,
        defaultValue: "received",
        field: "processing_status",
      },
      processingError: {
        type: DataTypes.TEXT,
        allowNull: true,
        defaultValue: null,
        field: "processing_error",
      },
      receivedAt: {
        type: DataTypes.DATE,
        allowNull: false,
        field: "received_at",
      },
    },
    {
      sequelize,
      tableName: "provider_webhook_logs",
      timestamps: true,
      underscored: true,
    }
  );
}
