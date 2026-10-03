import { readToken, writeToken } from './auth';

/**
 * Shared fetch wrapper for the ASP.NET API.
 *
 * The base URL comes from VITE_API_URL so the dashboard can be pointed at the
 * Cloudflare Workers gateway in production without a rebuild, and falls back to
 * the local API for development.
 */
export const API_BASE_URL: string =
  (import.meta.env.VITE_API_URL as string | undefined) ?? 'http://localhost:5000';

export class ApiError extends Error {
  constructor(message: string, readonly status: number) {
    super(message);
    this.name = 'ApiError';
  }
}

/** Notified when the API rejects our token, so the app can return to sign-in. */
type UnauthorizedHandler = () => void;
let onUnauthorized: UnauthorizedHandler = () => {};

export function setUnauthorizedHandler(handler: UnauthorizedHandler): void {
  onUnauthorized = handler;
}

function authHeaders(): Record<string, string> {
  const token = readToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function handle<T>(response: Response): Promise<T> {
  if (response.status === 401) {
    // The token is missing, expired or no longer valid. Clear it so the app
    // does not keep retrying with a credential the server has already refused.
    writeToken(null);
    onUnauthorized();
    throw new ApiError('Your session has expired. Please sign in again.', 401);
  }

  if (!response.ok) {
    // The API reports failures as { error: "..." }; fall back to the status
    // line when the body is empty or not JSON.
    const detail = await response.json().catch(() => null);
    throw new ApiError(detail?.error ?? `Request failed (${response.status})`, response.status);
  }

  if (response.status === 204) return undefined as T;

  return response.json() as Promise<T>;
}

export async function apiGet<T>(
  path: string,
  params?: Record<string, string | number | undefined>
): Promise<T> {
  const url = new URL(`${API_BASE_URL}${path}`);

  for (const [key, value] of Object.entries(params ?? {})) {
    if (value !== undefined && value !== '') url.searchParams.set(key, String(value));
  }

  return handle<T>(
    await fetch(url.toString(), {
      headers: { Accept: 'application/json', ...authHeaders() },
    })
  );
}

export async function apiPost<T>(path: string, body?: unknown): Promise<T> {
  return handle<T>(
    await fetch(`${API_BASE_URL}${path}`, {
      method: 'POST',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        ...authHeaders(),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  );
}
