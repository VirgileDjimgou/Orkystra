const legacyStorageKey = "fleetops.session";

/** Removes the pre-Sprint-12 JWT cache during the first protected-session boot. */
export function clearLegacyStoredSession() {
  if (typeof window !== "undefined") {
    window.localStorage.removeItem(legacyStorageKey);
  }
}
