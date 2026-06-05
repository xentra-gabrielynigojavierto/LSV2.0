'use server';

import { requireOrg } from '@/lib/auth-guards';
import { notificationsServerApi } from '@/lib/notifications-server-api';
import type { ProductType } from '@/lib/notifications-shared';

export type ActionResult<T = void> =
  | { success: true; data?: T }
  | { success: false; error: string };

export interface BrandingCreateInput {
  productType:     ProductType;
  brandName:       string;
  logoUrl?:        string | null;
  primaryColor?:   string | null;
  secondaryColor?: string | null;
  accentColor?:    string | null;
  textColor?:      string | null;
  backgroundColor?: string | null;
  buttonRadius?:   string | null;
  fontFamily?:     string | null;
  emailHeaderHtml?: string | null;
  emailFooterHtml?: string | null;
  supportEmail?:   string | null;
  supportPhone?:   string | null;
  websiteUrl?:     string | null;
}

export async function createBranding(
  input: BrandingCreateInput,
): Promise<ActionResult<{ id: string }>> {
  const session = await requireOrg();
  try {
    const res = await notificationsServerApi.brandingCreate(
      session.tenantId,
      input as unknown as Record<string, unknown>,
    );
    return { success: true, data: { id: res.data.id } };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create branding.' };
  }
}

export type BrandingUpdateInput = Partial<Omit<BrandingCreateInput, 'productType'>>;

export async function updateBranding(
  id: string,
  input: BrandingUpdateInput,
): Promise<ActionResult> {
  const session = await requireOrg();
  try {
    await notificationsServerApi.brandingUpdate(
      session.tenantId,
      id,
      input as unknown as Record<string, unknown>,
    );
    return { success: true };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update branding.' };
  }
}
