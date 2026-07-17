const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
export const cookieSessionMarker = "cookie-session";

let csrfToken: string | null = null;

export function setCsrfToken(token: string | null) {
  csrfToken = token;
}

type ApiRequestOptions = {
  method?: string;
  body?: unknown;
  contentType?: string;
  token?: string | null;
  responseType?: "json" | "text";
};

export async function apiRequest<T>(
  path: string,
  {
    method = "GET",
    body,
    contentType,
    token,
    responseType = "json",
  }: ApiRequestOptions = {},
): Promise<T> {
  const headers = new Headers();
  let requestBody: BodyInit | undefined;
  if (body !== undefined) {
    headers.set("Content-Type", contentType ?? "application/json");
    requestBody =
      contentType === "text/csv" || typeof body === "string"
        ? String(body)
        : JSON.stringify(body);
  }
  if (token && token !== cookieSessionMarker) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  if (csrfToken && !["GET", "HEAD", "OPTIONS"].includes(method.toUpperCase())) {
    headers.set("X-CSRF-Token", csrfToken);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    method,
    headers,
    body: requestBody,
    credentials: "include",
  });

  if (!response.ok) {
    if (response.status === 401 && typeof window !== "undefined") {
      window.dispatchEvent(new Event("fleetops:session-expired"));
    }
    const text = await response.text();
    throw new Error(text || `HTTP ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  if (responseType === "text") {
    return (await response.text()) as T;
  }

  return (await response.json()) as T;
}
