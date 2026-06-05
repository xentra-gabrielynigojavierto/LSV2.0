'use server';

import { createHmac } from 'crypto';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';
const INTERNAL_REQUEST_SECRET =
  process.env['PublicTrustBoundary__InternalRequestSecret'] ??
  process.env.INTERNAL_REQUEST_SECRET ??
  '';

function signTenantId(tenantId: string): string {
  if (!INTERNAL_REQUEST_SECRET) return '';
  return createHmac('sha256', INTERNAL_REQUEST_SECRET).update(tenantId).digest('base64');
}

function publicHeaders(tenantId: string): Record<string, string> {
  const sig = signTenantId(tenantId);
  return {
    'Content-Type': 'application/json',
    'X-Tenant-Id': tenantId,
    ...(sig ? { 'X-Tenant-Id-Sig': sig } : {}),
  };
}

export interface EnrollmentPrefill {
  providerId:   string;
  companyName:  string;
  companyType:  string;
  email:        string;
  phone:        string;
  addressLine1: string;
  city:         string;
  state:        string;
  postalCode:   string;
}

export async function fetchEnrollmentPrefill(
  providerId: string,
  tenantId: string,
): Promise<EnrollmentPrefill | null> {
  try {
    const res = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/enrollment/prefill/${providerId}`,
      { headers: publicHeaders(tenantId), cache: 'no-store' },
    );
    if (!res.ok) return null;
    return await res.json() as EnrollmentPrefill;
  } catch {
    return null;
  }
}

export interface SendOtpResult {
  ok:    boolean;
  error?: string;
}

export async function sendOtp(
  email: string,
  providerId: string,
  tenantId: string,
): Promise<SendOtpResult> {
  try {
    const res = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/enrollment/send-otp`,
      {
        method:  'POST',
        headers: publicHeaders(tenantId),
        body:    JSON.stringify({ email, providerId }),
      },
    );
    if (!res.ok) {
      const body = await res.json().catch(() => ({})) as { message?: string };
      return { ok: false, error: body.message ?? 'Failed to send verification code.' };
    }
    return { ok: true };
  } catch {
    return { ok: false, error: 'Network error. Please try again.' };
  }
}

export interface RegisterResult {
  ok:    boolean;
  error?: string;
}

export interface RegisterPayload {
  providerId:   string;
  companyName:  string;
  email:        string;
  password:     string;
  firstName:    string;
  lastName?:    string;
  phone?:       string;
  addressLine1?: string;
  city?:        string;
  state?:       string;
  postalCode?:  string;
  otpCode?:     string;
  tenantId:     string;
}

export interface FirmRegisterPayload {
  tenantId:     string;
  companyName:  string;
  email:        string;
  password:     string;
  firstName:    string;
  lastName?:    string;
  phone?:       string;
  addressLine1?: string;
  city?:        string;
  state?:       string;
  postalCode?:  string;
}

export async function registerFirmEnrollment(payload: FirmRegisterPayload): Promise<RegisterResult> {
  const { tenantId, ...rest } = payload;
  const body = { ...rest, tenantId };
  try {
    const res = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/enrollment/register-firm`,
      {
        method:  'POST',
        headers: publicHeaders(tenantId),
        body:    JSON.stringify(body),
      },
    );
    if (!res.ok) {
      const data = await res.json().catch(() => ({})) as { message?: string; detail?: string; error?: string };
      return { ok: false, error: data.message ?? data.detail ?? data.error ?? 'Registration failed. Please try again.' };
    }
    return { ok: true };
  } catch {
    return { ok: false, error: 'Network error. Please try again.' };
  }
}

export async function registerEnrollment(payload: RegisterPayload): Promise<RegisterResult> {
  const { tenantId, ...body } = payload;
  try {
    const res = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/enrollment/register`,
      {
        method:  'POST',
        headers: publicHeaders(tenantId),
        body:    JSON.stringify(body),
      },
    );
    if (!res.ok) {
      const data = await res.json().catch(() => ({})) as { message?: string };
      return { ok: false, error: data.message ?? 'Registration failed. Please try again.' };
    }
    return { ok: true };
  } catch {
    return { ok: false, error: 'Network error. Please try again.' };
  }
}
