import { Op } from "sequelize";
import { ContactSuppression } from "../models/contact-suppression.model";
import { SuppressionType, SuppressionSource, SuppressionStatus } from "../types";
import { normalizeContactValue } from "../shared/contact-normalizer";

export interface CreateContactSuppressionInput {
  tenantId: string;
  channel: string;
  contactValue: string;
  suppressionType: SuppressionType;
  reason: string;
  source: SuppressionSource;
  status?: SuppressionStatus;
  expiresAt?: Date | null;
  createdBy?: string | null;
  notes?: string | null;
}

export interface SuppressionFilter {
  tenantId?: string;
  channel?: string;
  contactValue?: string;
  status?: SuppressionStatus;
  suppressionType?: SuppressionType;
  limit?: number;
  offset?: number;
}

export class ContactSuppressionRepository {
  async create(input: CreateContactSuppressionInput): Promise<ContactSuppression> {
    const normalizedContactValue = normalizeContactValue(input.channel, input.contactValue);
    return ContactSuppression.create({
      ...input,
      contactValue: normalizedContactValue,
      status: input.status ?? "active",
      expiresAt: input.expiresAt ?? null,
      createdBy: input.createdBy ?? null,
      notes: input.notes ?? null,
    });
  }

  async findById(id: string): Promise<ContactSuppression | null> {
    return ContactSuppression.findByPk(id);
  }

  async findByIdAndTenant(id: string, tenantId: string): Promise<ContactSuppression | null> {
    return ContactSuppression.findOne({ where: { id, tenantId } });
  }

  async findActive(
    tenantId: string,
    channel: string,
    contactValue: string
  ): Promise<ContactSuppression[]> {
    const normalized = normalizeContactValue(channel, contactValue);
    const now = new Date();
    return ContactSuppression.findAll({
      where: {
        tenantId,
        channel,
        contactValue: normalized,
        status: "active",
        [Op.or]: [{ expiresAt: null }, { expiresAt: { [Op.gt]: now } }],
      },
      order: [["createdAt", "DESC"]],
    });
  }

  async findExistingActive(
    tenantId: string,
    channel: string,
    contactValue: string,
    suppressionType: SuppressionType
  ): Promise<ContactSuppression | null> {
    const normalized = normalizeContactValue(channel, contactValue);
    return ContactSuppression.findOne({
      where: { tenantId, channel, contactValue: normalized, suppressionType, status: "active" },
    });
  }

  async list(filter: SuppressionFilter): Promise<{ rows: ContactSuppression[]; count: number }> {
    const where: Record<string, unknown> = {};
    if (filter.tenantId !== undefined) where["tenantId"] = filter.tenantId;
    if (filter.channel) where["channel"] = filter.channel;
    if (filter.contactValue) where["contactValue"] = normalizeContactValue(filter.channel ?? "", filter.contactValue);
    if (filter.status) where["status"] = filter.status;
    if (filter.suppressionType) where["suppressionType"] = filter.suppressionType;
    const limit = Math.min(filter.limit ?? 100, 500);
    const offset = filter.offset ?? 0;
    return ContactSuppression.findAndCountAll({ where, limit, offset, order: [["createdAt", "DESC"]] });
  }

  async update(
    id: string,
    updates: Partial<{ status: SuppressionStatus; notes: string | null; expiresAt: Date | null }>
  ): Promise<void> {
    await ContactSuppression.update(updates, { where: { id } });
  }

  /**
   * Create a suppression only if no active suppression of the same type already exists.
   * Returns the suppression (new or existing) and whether it was newly created.
   */
  async upsertFromEvent(input: CreateContactSuppressionInput): Promise<{ record: ContactSuppression; created: boolean }> {
    const existing = await this.findExistingActive(
      input.tenantId,
      input.channel,
      input.contactValue,
      input.suppressionType
    );
    if (existing) return { record: existing, created: false };
    const created = await this.create(input);
    return { record: created, created: true };
  }
}
