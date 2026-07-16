export type AuthenticatedUser = {
  userId: string;
  email: string;
  fullName: string;
  organizationName: string;
  driverId?: string | null;
  roles: string[];
  twoFactorEnabled?: boolean;
};

export type LoginResponse = {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthenticatedUser;
  requiresTwoFactor?: boolean;
  twoFactorProvider?: string | null;
  challengeMessage?: string | null;
};

export type LoginRequest = {
  email: string;
  password: string;
  twoFactorCode?: string;
};
