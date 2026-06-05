import { TenantContactPolicy } from "../models/tenant-contact-policy.model";

export interface ContactPolicyDefaults {
  blockSuppressedContacts: boolean;
  blockUnsubscribedContacts: boolean;
  blockComplainedContacts: boolean;
  blockBouncedContacts: boolean;
  blockInvalidContacts: boolean;
  blockCarrierRejectedContacts: boolean;
  allowManualOverride: boolean;
}

export const DEFAULT_CONTACT_POLICY: ContactPolicyDefaults = {
  blockSuppressedContacts: true,
  blockUnsubscribedContacts: true,
  blockComplainedContacts: true,
  blockBouncedContacts: false,
  blockInvalidContacts: false,
  blockCarrierRejectedContacts: false,
  allowManualOverride: false,
};

export class TenantContactPolicyRepository {
  async findById(id: string): Promise<TenantContactPolicy | null> {
    return TenantContactPolicy.findByPk(id);
  }

  async findByIdAndTenant(id: string, tenantId: string | undefined): Promise<TenantContactPolicy | null> {
    const where: Record<string, unknown> = { id };
    if (tenantId !== undefined) where["tenantId"] = tenantId;
    return TenantContactPolicy.findOne({ where });
  }

  async findAllByTenant(tenantId: string | undefined): Promise<TenantContactPolicy[]> {
    const where: Record<string, unknown> = {};
    if (tenantId !== undefined) where["tenantId"] = tenantId;
    return TenantContactPolicy.findAll({ where, order: [["createdAt", "DESC"]] });
  }

  /**
   * Resolve effective policy for a tenant + channel.
   * Channel-specific policy takes precedence over global (channel=null).
   * Returns null if no policy found (caller should use DEFAULT_CONTACT_POLICY).
   */
  async findEffectivePolicy(tenantId: string, channel?: string | null): Promise<TenantContactPolicy | null> {
    // Channel-specific first
    if (channel) {
      const channelPolicy = await TenantContactPolicy.findOne({
        where: { tenantId, channel, status: "active" },
        order: [["createdAt", "DESC"]],
      });
      if (channelPolicy) return channelPolicy;
    }
    // Fall back to global
    return TenantContactPolicy.findOne({
      where: { tenantId, channel: null, status: "active" },
      order: [["createdAt", "DESC"]],
    });
  }

  async create(input: {
    tenantId: string;
    channel?: string | null;
    blockSuppressedContacts?: boolean;
    blockUnsubscribedContacts?: boolean;
    blockComplainedContacts?: boolean;
    blockBouncedContacts?: boolean;
    blockInvalidContacts?: boolean;
    blockCarrierRejectedContacts?: boolean;
    allowManualOverride?: boolean;
    status?: "active" | "inactive";
  }): Promise<TenantContactPolicy> {
    return TenantContactPolicy.create({
      ...DEFAULT_CONTACT_POLICY,
      ...input,
      channel: input.channel ?? null,
      status: input.status ?? "active",
    });
  }

  async update(
    id: string,
    updates: Partial<{
      channel: string | null;
      blockSuppressedContacts: boolean;
      blockUnsubscribedContacts: boolean;
      blockComplainedContacts: boolean;
      blockBouncedContacts: boolean;
      blockInvalidContacts: boolean;
      blockCarrierRejectedContacts: boolean;
      allowManualOverride: boolean;
      status: "active" | "inactive";
    }>
  ): Promise<void> {
    await TenantContactPolicy.update(updates, { where: { id } });
  }
}
