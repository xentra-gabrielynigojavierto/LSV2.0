import { Tenant } from "../models/tenant.model";

export class TenantRepository {
  async findById(id: string): Promise<Tenant | null> {
    return Tenant.findByPk(id);
  }

  async findAll(): Promise<Tenant[]> {
    return Tenant.findAll();
  }

  async create(data: { name: string }): Promise<Tenant> {
    return Tenant.create(data);
  }
}
