import { defineStore } from "pinia";
import { apiRequest } from "../../services/api";
import type {
  AuthenticatedUser,
  LoginRequest,
  LoginResponse,
} from "./contracts";
import {
  clearStoredSession,
  readStoredSession,
  writeStoredSession,
  type StoredSession,
} from "./session";

type SessionStatus = "anonymous" | "authenticating" | "authenticated";
type ExtendedSessionStatus = SessionStatus | "twoFactorRequired";

export const useSessionStore = defineStore("session", {
  state: () => ({
    status: "anonymous" as ExtendedSessionStatus,
    accessToken: null as string | null,
    expiresAtUtc: null as string | null,
    user: null as AuthenticatedUser | null,
    error: "",
  }),
  getters: {
    isAuthenticated: (state) =>
      state.status === "authenticated" && !!state.accessToken,
    isAdmin: (state) => !!state.user?.roles.includes("Admin"),
    canManageUsers(): boolean {
      return this.isAdmin;
    },
    canManageIntegrations(): boolean {
      return this.isAdmin;
    },
  },
  actions: {
    hydrate() {
      const session = readStoredSession();
      if (!session) {
        this.reset();
        return;
      }

      this.applySession(session);
    },
    async login(payload: LoginRequest) {
      this.status = "authenticating";
      this.error = "";

      try {
        const response = await apiRequest<LoginResponse>("/api/auth/login", {
          method: "POST",
          body: payload,
        });

        if (response.requiresTwoFactor) {
          this.status = "twoFactorRequired";
          this.accessToken = null;
          this.expiresAtUtc = null;
          this.user = null;
          this.error =
            response.challengeMessage ??
            "Enter the 6-digit code from your authenticator app.";
          clearStoredSession();
          return response;
        }

        this.applySession(response);
        return response;
      } catch (error) {
        this.reset();
        this.error =
          error instanceof Error
            ? "Sign-in failed. Check your credentials."
            : "Sign-in failed.";
        throw error;
      }
    },
    async refreshProfile() {
      if (!this.accessToken) {
        this.reset();
        return;
      }

      try {
        const user = await apiRequest<AuthenticatedUser>("/api/auth/me", {
          token: this.accessToken,
        });
        this.user = user;
        this.status = "authenticated";
        if (this.expiresAtUtc) {
          writeStoredSession({
            accessToken: this.accessToken,
            expiresAtUtc: this.expiresAtUtc,
            user,
          });
        }
      } catch {
        this.reset();
      }
    },
    logout() {
      this.reset();
    },
    reset() {
      this.status = "anonymous";
      this.accessToken = null;
      this.expiresAtUtc = null;
      this.user = null;
      clearStoredSession();
    },
    applySession(session: StoredSession | LoginResponse) {
      this.status = "authenticated";
      this.accessToken = session.accessToken;
      this.expiresAtUtc = session.expiresAtUtc;
      this.user = session.user;
      this.error = "";
      writeStoredSession({
        accessToken: session.accessToken,
        expiresAtUtc: session.expiresAtUtc,
        user: session.user,
      });
    },
  },
});
