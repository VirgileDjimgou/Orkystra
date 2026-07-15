export type AuthenticatedUser = {
  userId: string;
  email: string;
  fullName: string;
  organizationName: string;
  roles: string[];
};

export type LoginResponse = {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthenticatedUser;
};

export type LoginRequest = {
  email: string;
  password: string;
};
