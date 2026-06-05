'use server';

import { requirePlatformAdmin } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';

export interface InvitePlatformUserResult {
  success:         boolean;
  activationLink?: string;
  error?:          string;
}

export async function invitePlatformUserAction(payload: {
  email:     string;
  firstName: string;
  lastName:  string;
  roleId?:   string;
}): Promise<InvitePlatformUserResult> {
  await requirePlatformAdmin();

  try {
    const result = await controlCenterServerApi.users.invitePlatformUser(payload);
    return {
      success:         true,
      activationLink:  result.activationLink,
    };
  } catch (err) {
    return {
      success: false,
      error:   err instanceof Error ? err.message : 'Failed to invite platform user.',
    };
  }
}
