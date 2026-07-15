import type { AuthenticatedUser } from "./contracts";

export type StoredSession = {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthenticatedUser;
};

const storageKey = "fleetops.session";

export function readStoredSession(): StoredSession | null {
  if (typeof window === "undefined") {
    return null;
  }

  const raw = window.localStorage.getItem(storageKey);
  if (!raw) {
    return null;
  }

  try {
    const session = JSON.parse(raw) as StoredSession;
    if (Date.parse(session.expiresAtUtc) <= Date.now()) {
      clearStoredSession();
      return null;
    }
    return session;
  } catch {
    clearStoredSession();
    return null;
  }
}

export function writeStoredSession(session: StoredSession) {
  window.localStorage.setItem(storageKey, JSON.stringify(session));
}

export function clearStoredSession() {
  window.localStorage.removeItem(storageKey);
}
