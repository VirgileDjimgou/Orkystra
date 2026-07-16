export type MfaStatus = {
  isEnabled: boolean;
  hasSharedKey: boolean;
  accountEmail: string;
};

export type MfaSetup = {
  isEnabled: boolean;
  sharedKey: string;
  manualEntryKey: string;
  authenticatorUri: string;
};

export type VerifyMfaResponse = {
  isEnabled: boolean;
  recoveryCodes: string[];
};

export type DataLifecycleCount = {
  key: string;
  label: string;
  count: number;
};

export type DataLifecycleCategory = {
  key: string;
  label: string;
  description: string;
};

export type DataLifecycleSummary = {
  generatedAtUtc: string;
  organizationName: string;
  organizationSlug: string;
  trackingRetentionDays: number;
  counts: DataLifecycleCount[];
  categories: DataLifecycleCategory[];
};

export type PurgeLifecycleDataRequest = {
  confirmation: string;
  cutoffUtc: string;
  categories: string[];
};

export type PurgeLifecycleDataResponse = {
  cutoffUtc: string;
  totalDeleted: number;
  results: Array<{
    key: string;
    deletedCount: number;
  }>;
};
