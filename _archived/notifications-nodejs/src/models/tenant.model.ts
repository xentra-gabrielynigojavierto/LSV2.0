import { DataTypes, Model, Sequelize, Optional } from "sequelize";

interface TenantAttributes {
  id: string;
  name: string;
  createdAt?: Date;
  updatedAt?: Date;
}

interface TenantCreationAttributes extends Optional<TenantAttributes, "id"> {}

export class Tenant extends Model<TenantAttributes, TenantCreationAttributes> {
  declare id: string;
  declare name: string;
  declare createdAt: Date;
  declare updatedAt: Date;
}

export function initTenantModel(sequelize: Sequelize): void {
  Tenant.init(
    {
      id: {
        type: DataTypes.UUID,
        defaultValue: DataTypes.UUIDV4,
        primaryKey: true,
      },
      name: {
        type: DataTypes.STRING,
        allowNull: false,
      },
    },
    {
      sequelize,
      tableName: "tenants",
      timestamps: true,
    }
  );
}
