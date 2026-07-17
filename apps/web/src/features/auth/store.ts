import { defineStore } from "pinia";
import {
  apiRequest,
  cookieSessionMarker,
  setCsrfToken,
} from "../../services/api";
import type {
  AuthenticatedUser,
  CsrfTokenResponse,
  LoginRequest,
  LoginResponse,
} from "./contracts";
import { clearLegacyStoredSession } from "./session";

type SessionStatus =
  "anonymous" | "authenticating" | "authenticated" | "twoFactorRequired";

export const useSessionStore = defineStore("session", {
  state: () => ({
    status: "anonymous" as SessionStatus,
    // Compatibility marker for existing feature stores. It is never a credential.
    accessToken: null as string | null,
    expiresAtUtc: null as string | null,
    user: null as AuthenticatedUser | null,
    initialized: false,
    monitoringSession: false,
    error: "",
  }),
  getters: {
    isAuthenticated: (state) =>
      state.status === "authenticated" &&
      state.accessToken === cookieSessionMarker,
    isAdmin: (state) => !!state.user?.roles.includes("Admin"),
    canManageUsers(): boolean {
      return this.isAdmin;
    },
    canManageIntegrations(): boolean {
      return this.isAdmin;
    },
  },
  actions: {
    async hydrate() {
      if (this.initialized) {
        return;
      }

      this.startSessionMonitoring();
      clearLegacyStoredSession();
      this.status = "authenticating";
      try {
        const user = await apiRequest<AuthenticatedUser>("/api/v1/auth/me");
        const csrf = await apiRequest<CsrfTokenResponse>("/api/v1/auth/csrf");
        setCsrfToken(csrf.csrfToken);
        this.status = "authenticated";
        this.accessToken = cookieSessionMarker;
        this.user = user;
        this.error = "";
      } catch {
        this.reset();
      } finally {
        this.initialized = true;
      }
    },
    async login(payload: LoginRequest) {
      this.startSessionMonitoring();
      this.status = "authenticating";
      this.error = "";

      try {
        const response = await apiRequest<LoginResponse>(
          "/api/v1/auth/web/login",
          {
            method: "POST",
            body: payload,
          },
        );

        if (response.requiresTwoFactor) {
          this.status = "twoFactorRequired";
          this.accessToken = null;
          this.expiresAtUtc = null;
          this.user = null;
          this.error =
            response.challengeMessage ??
            "Enter the 6-digit code from your authenticator app.";
          setCsrfToken(null);
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
      try {
        this.user = await apiRequest<AuthenticatedUser>("/api/v1/auth/me");
        this.status = "authenticated";
        this.accessToken = cookieSessionMarker;
      } catch {
        this.reset();
      }
    },
    async logout() {
      try {
        if (this.isAuthenticated) {
          await apiRequest<void>("/api/v1/auth/logout", { method: "POST" });
        }
      } finally {
        this.reset();
      }
    },
    reset() {
      this.status = "anonymous";
      this.accessToken = null;
      this.expiresAtUtc = null;
      this.user = null;
      this.initialized = true;
      setCsrfToken(null);
      clearLegacyStoredSession();
    },
    applySession(session: LoginResponse) {
      this.status = "authenticated";
      this.accessToken = cookieSessionMarker;
      this.expiresAtUtc = session.expiresAtUtc;
      this.user = session.user;
      this.initialized = true;
      this.error = "";
      setCsrfToken(session.csrfToken ?? null);
      clearLegacyStoredSession();
    },
    startSessionMonitoring() {
      if (this.monitoringSession || typeof window === "undefined") {
        return;
      }

      window.addEventListener("fleetops:session-expired", () => this.reset());
      this.monitoringSession = true;
    },
  },
});
