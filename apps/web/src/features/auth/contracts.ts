export type AuthenticatedUser = {
  userId: string;
  email: string;
  fullName: string;
  organizationName: string;
  driverId?: string | null;
  roles: string[];
  twoFactorEnabled?: boolean;
  isDemo?: boolean;
};

export type DemoSessionState = {
  scenario: string;
  status: string;
  revision: number;
  updatedAtUtc: string;
  expiresAtUtc: string;
};

export type DemoLaunchResponse = LoginResponse & {
  label: string;
  scenario: DemoSessionState;
};

export type LoginResponse = {
  /** Legacy test-fixture field; the protected Web login never returns a credential. */
  accessToken?: string;
  expiresAtUtc: string;
  user: AuthenticatedUser;
  csrfToken?: string;
  requiresTwoFactor?: boolean;
  twoFactorProvider?: string | null;
  challengeMessage?: string | null;
};

export type CsrfTokenResponse = {
  csrfToken: string;
};

export type LoginRequest = {
  email: string;
  password: string;
  twoFactorCode?: string;
};
