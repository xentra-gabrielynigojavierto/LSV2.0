'use client';

import { useState, useCallback, useRef, useEffect, type FormEvent } from 'react';
import { sendOtp, registerEnrollment, registerFirmEnrollment, type EnrollmentPrefill } from './actions';
import { useRouter } from 'next/navigation';

// ── Address suggestion (from /api/geocode/address) ────────────────────────────

interface AddressSuggestion {
  displayName: string;
  addressLine1: string;
  city:         string;
  state:        string;
  postalCode:   string;
}

// ── Props ─────────────────────────────────────────────────────────────────────

interface ReferralPrefill {
  companyName: string;
  email:       string;
  phone:       string;
  firstName:   string;
  lastName:    string;
}

interface EnrollmentFormProps {
  prefill:           EnrollmentPrefill | null;
  providerId:        string | null;
  tenantId:          string | null;
  referralPrefill:   ReferralPrefill | null;
  isFirmEnrollment?: boolean;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function EnrollmentForm({ prefill, providerId, tenantId, referralPrefill, isFirmEnrollment = false }: EnrollmentFormProps) {
  const router = useRouter();

  // Form fields — provider prefill wins over referral prefill; referral prefill wins over empty
  const [companyName,  setCompanyName]  = useState(prefill?.companyName  ?? referralPrefill?.companyName ?? '');
  const companyType  = prefill?.companyType ?? 'LawFirm';
  const [email,        setEmail]        = useState(prefill?.email        ?? referralPrefill?.email       ?? '');
  const [phone,        setPhone]        = useState(prefill?.phone        ?? referralPrefill?.phone       ?? '');
  const [addressLine1, setAddressLine1] = useState(prefill?.addressLine1 ?? '');
  const [city,         setCity]         = useState(prefill?.city         ?? '');
  const [state,        setState]        = useState(prefill?.state        ?? '');
  const [postalCode,   setPostalCode]   = useState(prefill?.postalCode   ?? '');
  const [firstName,    setFirstName]    = useState(referralPrefill?.firstName ?? '');
  const [lastName,     setLastName]     = useState(referralPrefill?.lastName  ?? '');
  const [password,     setPassword]     = useState('');
  const [confirmPwd,   setConfirmPwd]   = useState('');
  const [agreeTerms,   setAgreeTerms]   = useState(false);

  // OTP state — firm enrollments skip OTP (new account, no existing email on record)
  const originalEmail = prefill?.email ?? referralPrefill?.email ?? '';
  const emailChanged  = !isFirmEnrollment && email.trim().toLowerCase() !== originalEmail.trim().toLowerCase();
  const [otpSent,      setOtpSent]      = useState(false);
  const [otpCode,      setOtpCode]      = useState('');
  const [otpVerified,  setOtpVerified]  = useState(false);
  const [sendingOtp,   setSendingOtp]   = useState(false);
  const [otpError,     setOtpError]     = useState('');

  // Address autocomplete
  const [addressSuggestions, setAddressSuggestions] = useState<AddressSuggestion[]>([]);
  const [showSuggestions,    setShowSuggestions]    = useState(false);
  const [addressLoading,     setAddressLoading]     = useState(false);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Password visibility
  const [showPwd,     setShowPwd]     = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);

  // Submission
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState('');

  // ── Address autocomplete ─────────────────────────────────────────────────

  const handleAddressInput = useCallback((value: string) => {
    setAddressLine1(value);
    setShowSuggestions(false);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (value.trim().length < 4) { setAddressSuggestions([]); return; }

    debounceRef.current = setTimeout(async () => {
      setAddressLoading(true);
      try {
        const q   = encodeURIComponent(value);
        const res = await fetch(`/api/geocode/address?q=${q}`);
        if (res.ok) {
          const data = await res.json() as AddressSuggestion[];
          setAddressSuggestions(data.slice(0, 5));
          setShowSuggestions(data.length > 0);
        }
      } catch { /* ignore */ } finally { setAddressLoading(false); }
    }, 350);
  }, []);

  const applySuggestion = (s: AddressSuggestion) => {
    setAddressLine1(s.addressLine1 || s.displayName);
    setCity(s.city);
    setState(s.state);
    setPostalCode(s.postalCode);
    setAddressSuggestions([]);
    setShowSuggestions(false);
  };

  // ── OTP flow ─────────────────────────────────────────────────────────────

  // Reset OTP state when email changes after already having sent OTP
  useEffect(() => {
    if (otpSent || otpVerified) {
      setOtpSent(false);
      setOtpVerified(false);
      setOtpCode('');
      setOtpError('');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [email]);

  const handleSendOtp = async () => {
    if (!tenantId || !providerId) return;
    setOtpError('');
    setSendingOtp(true);
    const result = await sendOtp(email, providerId, tenantId);
    setSendingOtp(false);
    if (result.ok) {
      setOtpSent(true);
    } else {
      setOtpError(result.error ?? 'Failed to send code.');
    }
  };

  // When OTP input has 6 digits assume it's ready (visual only; backend verifies)
  const otpReady = otpCode.trim().length === 6;

  // ── Validation ────────────────────────────────────────────────────────────

  function validate(): string | null {
    if (!companyName.trim()) return 'Company name is required.';
    if (!email.trim())       return 'Email is required.';
    if (!firstName.trim())   return 'First name is required.';
    if (!password)           return 'Password is required.';
    if (password.length < 8) return 'Password must be at least 8 characters.';
    if (password !== confirmPwd) return 'Passwords do not match.';
    if (!agreeTerms) return 'You must agree to the Terms & Conditions to continue.';
    if (!isFirmEnrollment && emailChanged && !otpCode.trim()) return 'Enter the verification code sent to your new email address.';
    return null;
  }

  // ── Form submit ───────────────────────────────────────────────────────────

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitError('');

    const validationError = validate();
    if (validationError) { setSubmitError(validationError); return; }

    setSubmitting(true);

    let result;
    if (isFirmEnrollment && tenantId) {
      // Law firm coming from referral status page — use firm-specific enrollment endpoint
      result = await registerFirmEnrollment({
        tenantId,
        companyName:  companyName.trim(),
        email:        email.trim(),
        password,
        firstName:    firstName.trim(),
        lastName:     lastName.trim() || undefined,
        phone:        phone.trim() || undefined,
        addressLine1: addressLine1.trim() || undefined,
        city:         city.trim() || undefined,
        state:        state.trim() || undefined,
        postalCode:   postalCode.trim() || undefined,
      });
    } else if (providerId && tenantId) {
      // Provider self-enrollment from network directory
      result = await registerEnrollment({
        providerId,
        companyName:  companyName.trim(),
        email:        email.trim(),
        password,
        firstName:    firstName.trim(),
        lastName:     lastName.trim() || undefined,
        phone:        phone.trim() || undefined,
        addressLine1: addressLine1.trim() || undefined,
        city:         city.trim() || undefined,
        state:        state.trim() || undefined,
        postalCode:   postalCode.trim() || undefined,
        otpCode:      emailChanged ? otpCode.trim() : undefined,
        tenantId,
      });
    } else {
      setSubmitting(false);
      setSubmitError('Missing provider or tenant context. Please return to the network directory and try again.');
      return;
    }

    setSubmitting(false);

    if (result.ok) {
      router.push('/enroll/welcome');
    } else {
      setSubmitError(result.error ?? 'Registration failed. Please try again.');
    }
  };

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <form onSubmit={handleSubmit} className="bg-white rounded-2xl shadow-sm border border-gray-200 p-6 space-y-5">

      {/* ── Company section ─────────────────────────────────────────────── */}
      <div>
        <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide mb-3">
          Organization
        </h2>
        <div className="grid grid-cols-2 gap-4">
          <div className="col-span-2 sm:col-span-1">
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Company Name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={companyName}
              onChange={e => setCompanyName(e.target.value)}
              placeholder="Your organization name"
              required
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div className="col-span-2 sm:col-span-1">
            <label className="block text-sm font-medium text-gray-700 mb-1">Company Type</label>
            <div className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-gray-50 text-gray-500">
              {companyType}
            </div>
          </div>
        </div>
      </div>

      {/* ── Contact section ─────────────────────────────────────────────── */}
      <div>
        <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide mb-3">
          Contact Information
        </h2>
        <div className="space-y-4">

          {/* Email row */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Email Address <span className="text-red-500">*</span>
            </label>
            <div className="flex gap-2">
              <input
                type="email"
                value={email}
                onChange={e => setEmail(e.target.value)}
                placeholder="you@example.com"
                required
                className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              {emailChanged && (
                <button
                  type="button"
                  onClick={handleSendOtp}
                  disabled={sendingOtp || !email.trim()}
                  className="flex-shrink-0 px-3 py-2 rounded-lg text-sm font-medium bg-blue-50 text-blue-700 hover:bg-blue-100 disabled:opacity-50 transition-colors"
                >
                  {sendingOtp ? 'Sending…' : otpSent ? 'Resend Code' : 'Verify'}
                </button>
              )}
            </div>
            {emailChanged && (
              <p className="text-xs text-amber-600 mt-1">
                <i className="ri-information-line mr-1" />
                You changed your email. Click <strong>Verify</strong> to receive a confirmation code.
              </p>
            )}
          </div>

          {/* OTP input — shown when email changed + OTP has been sent */}
          {emailChanged && otpSent && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Verification Code <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                inputMode="numeric"
                maxLength={6}
                value={otpCode}
                onChange={e => setOtpCode(e.target.value.replace(/\D/g, ''))}
                placeholder="6-digit code"
                className="w-40 border border-gray-300 rounded-lg px-3 py-2 text-sm font-mono tracking-widest focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <p className="text-xs text-gray-400 mt-1">Check your email — the code expires in 10 minutes.</p>
              {otpError && <p className="text-xs text-red-500 mt-1">{otpError}</p>}
            </div>
          )}

          {/* Phone */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Phone Number</label>
            <input
              type="tel"
              value={phone}
              onChange={e => setPhone(e.target.value)}
              placeholder="(555) 000-0000"
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>
      </div>

      {/* ── Address section ──────────────────────────────────────────────── */}
      <div>
        <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide mb-3">
          Address
        </h2>
        <div className="space-y-4">
          {/* Address Line 1 with autocomplete */}
          <div className="relative">
            <label className="block text-sm font-medium text-gray-700 mb-1">Street Address</label>
            <input
              type="text"
              value={addressLine1}
              onChange={e => handleAddressInput(e.target.value)}
              onBlur={() => setTimeout(() => setShowSuggestions(false), 200)}
              onFocus={() => addressSuggestions.length > 0 && setShowSuggestions(true)}
              placeholder="123 Main St"
              autoComplete="off"
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            {addressLoading && (
              <div className="absolute right-3 top-9 text-gray-400">
                <i className="ri-loader-4-line animate-spin text-sm" />
              </div>
            )}
            {showSuggestions && addressSuggestions.length > 0 && (
              <ul className="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
                {addressSuggestions.map((s, i) => (
                  <li
                    key={i}
                    onMouseDown={() => applySuggestion(s)}
                    className="px-3 py-2 text-sm cursor-pointer hover:bg-blue-50 text-gray-700"
                  >
                    {s.displayName}
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div className="col-span-1">
              <label className="block text-sm font-medium text-gray-700 mb-1">City</label>
              <input
                type="text"
                value={city}
                onChange={e => setCity(e.target.value)}
                placeholder="City"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">State</label>
              <input
                type="text"
                value={state}
                onChange={e => setState(e.target.value)}
                placeholder="CA"
                maxLength={2}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">ZIP</label>
              <input
                type="text"
                value={postalCode}
                onChange={e => setPostalCode(e.target.value)}
                placeholder="90210"
                maxLength={10}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>
        </div>
      </div>

      {/* ── Account section ──────────────────────────────────────────────── */}
      <div>
        <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide mb-3">
          Your Account
        </h2>
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                First Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={firstName}
                onChange={e => setFirstName(e.target.value)}
                placeholder="First"
                required
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Last Name</label>
              <input
                type="text"
                value={lastName}
                onChange={e => setLastName(e.target.value)}
                placeholder="Last"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>

          {/* Password */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Password <span className="text-red-500">*</span>
            </label>
            <div className="relative">
              <input
                type={showPwd ? 'text' : 'password'}
                autoComplete="new-password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                placeholder="At least 8 characters"
                required
                minLength={8}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 pr-10 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <button
                type="button"
                onClick={() => setShowPwd(v => !v)}
                className="absolute right-3 top-2.5 text-gray-400 hover:text-gray-600"
              >
                <i className={showPwd ? 'ri-eye-off-line' : 'ri-eye-line'} />
              </button>
            </div>
          </div>

          {/* Confirm password */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Confirm Password <span className="text-red-500">*</span>
            </label>
            <div className="relative">
              <input
                type={showConfirm ? 'text' : 'password'}
                autoComplete="new-password"
                value={confirmPwd}
                onChange={e => setConfirmPwd(e.target.value)}
                placeholder="Re-enter password"
                required
                className={[
                  'w-full border rounded-lg px-3 py-2 pr-10 text-sm focus:outline-none focus:ring-2',
                  confirmPwd && password !== confirmPwd
                    ? 'border-red-300 focus:ring-red-400'
                    : 'border-gray-300 focus:ring-blue-500',
                ].join(' ')}
              />
              <button
                type="button"
                onClick={() => setShowConfirm(v => !v)}
                className="absolute right-3 top-2.5 text-gray-400 hover:text-gray-600"
              >
                <i className={showConfirm ? 'ri-eye-off-line' : 'ri-eye-line'} />
              </button>
            </div>
            {confirmPwd && password !== confirmPwd && (
              <p className="text-xs text-red-500 mt-1">Passwords do not match.</p>
            )}
          </div>
        </div>
      </div>

      {/* ── Terms & Conditions ───────────────────────────────────────────── */}
      <div className="flex items-start gap-3 pt-1">
        <input
          type="checkbox"
          id="agreeTerms"
          checked={agreeTerms}
          onChange={e => setAgreeTerms(e.target.checked)}
          className="mt-0.5 h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
        />
        <label htmlFor="agreeTerms" className="text-sm text-gray-600 leading-snug">
          I agree to the{' '}
          <a href="/terms" target="_blank" className="text-blue-600 hover:underline">
            Terms of Service
          </a>{' '}
          and{' '}
          <a href="/privacy" target="_blank" className="text-blue-600 hover:underline">
            Privacy Policy
          </a>
          . I confirm I am authorized to create this account on behalf of the organization.
        </label>
      </div>

      {/* ── Error message ─────────────────────────────────────────────────── */}
      {submitError && (
        <div className="flex items-start gap-2 bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          <i className="ri-error-warning-line flex-shrink-0 mt-0.5" />
          <span>{submitError}</span>
        </div>
      )}

      {/* ── Submit ───────────────────────────────────────────────────────── */}
      <button
        type="submit"
        disabled={submitting}
        className="w-full py-3 px-6 rounded-xl bg-blue-600 text-white font-semibold text-sm hover:bg-blue-700 disabled:opacity-60 disabled:cursor-not-allowed transition-colors flex items-center justify-center gap-2"
      >
        {submitting ? (
          <><i className="ri-loader-4-line animate-spin" />Creating your account…</>
        ) : (
          <><i className="ri-shield-check-line" />Activate My Portal Access</>
        )}
      </button>
    </form>
  );
}
