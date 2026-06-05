export interface TwilioCredentials {
  accountSid: string;
  authToken: string;
}

export interface TwilioEndpointConfig {
  fromNumber: string;
  messagingServiceSid?: string;
}

export interface TwilioWebhookConfig {
  authToken?: string;
}

export const TWILIO_REQUIRED_CREDENTIAL_FIELDS: (keyof TwilioCredentials)[] = ["accountSid", "authToken"];
export const TWILIO_REQUIRED_ENDPOINT_FIELDS: (keyof TwilioEndpointConfig)[] = ["fromNumber"];

export function validateTwilioCredentials(creds: Record<string, unknown>): string[] {
  const errors: string[] = [];
  if (!creds["accountSid"] || typeof creds["accountSid"] !== "string") errors.push("accountSid is required");
  else if (!(creds["accountSid"] as string).startsWith("AC")) errors.push("accountSid must start with 'AC'");
  if (!creds["authToken"] || typeof creds["authToken"] !== "string") errors.push("authToken is required");
  return errors;
}

export function validateTwilioEndpointConfig(config: Record<string, unknown>): string[] {
  const errors: string[] = [];
  const phoneRegex = /^\+?[1-9]\d{6,14}$/;
  if (!config["fromNumber"] || typeof config["fromNumber"] !== "string") {
    errors.push("fromNumber is required");
  } else if (!phoneRegex.test(config["fromNumber"] as string)) {
    errors.push("fromNumber must be a valid E.164 phone number");
  }
  return errors;
}

export const TWILIO_CATALOG = {
  providerType: "twilio",
  channel: "sms",
  displayName: "Twilio",
  credentialFields: [
    { name: "accountSid", label: "Account SID", secret: false, required: true },
    { name: "authToken", label: "Auth Token", secret: true, required: true },
  ],
  endpointFields: [
    { name: "fromNumber", label: "From Phone Number (E.164)", secret: false, required: true },
    { name: "messagingServiceSid", label: "Messaging Service SID (optional)", secret: false, required: false },
  ],
};
