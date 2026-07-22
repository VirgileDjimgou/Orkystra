export type ApiClientCredentialType = "Partner" | "Device";

export type ApiClientCredential = {
  id: string;
  name: string;
  credentialType: ApiClientCredentialType;
  scopes: string[];
  keyId: string;
  secretPreview: string;
  isActive: boolean;
  lastUsedAtUtc: string | null;
  revokedAtUtc: string | null;
  rowVersion: number;
};

export type CreatedApiClientCredential = {
  id: string;
  name: string;
  credentialType: ApiClientCredentialType;
  scopes: string[];
  keyId: string;
  plainTextSecret: string;
  secretPreview: string;
  isActive: boolean;
  rowVersion: number;
};

export type WebhookEndpoint = {
  id: string;
  name: string;
  eventType: string;
  targetUrl: string;
  isActive: boolean;
  isSandbox: boolean;
  lastSucceededAtUtc: string | null;
  disabledAtUtc: string | null;
  rowVersion: number;
};

export type IntegrationContract = {
  eventType: string;
  description: string;
  examplePayload: Record<string, unknown>;
};

export type IntegrationOutboxMessage = {
  id: string;
  webhookEndpointId: string;
  eventType: string;
  aggregateType: string;
  aggregateId: string;
  status: string;
  attemptCount: number;
  occurredAtUtc: string;
  nextAttemptAtUtc: string;
  deliveredAtUtc: string | null;
  deadLetteredAtUtc: string | null;
  lastError: string | null;
};

export type CreateApiClientCredentialRequest = {
  name: string;
  credentialType: ApiClientCredentialType;
  scopes: string[];
};

export type CreateWebhookEndpointRequest = {
  name: string;
  eventType: string;
  targetUrl: string | null;
  signingSecret: string;
  isSandbox: boolean;
};

export type SandboxTelematicsConnection = {
  id: string;
  name: string;
  isActive: boolean;
  lastSucceededAtUtc: string | null;
  lastError: string | null;
  resumeCursor: string | null;
  rowVersion: number;
};
