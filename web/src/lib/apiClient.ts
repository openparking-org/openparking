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

export async function apiGet<T>(path: string, params?: Record<string, string | number | undefined>): Promise<T> {
  const url = new URL(`${API_BASE_URL}${path}`);

  for (const [key, value] of Object.entries(params ?? {})) {
    if (value !== undefined && value !== '') url.searchParams.set(key, String(value));
  }

  const response = await fetch(url.toString(), {
    headers: { Accept: 'application/json' },
  });

  if (!response.ok) {
    // The API reports failures as { error: "..." }; fall back to the status
    // line when the body is empty or not JSON.
    const detail = await response.json().catch(() => null);
    throw new ApiError(detail?.error ?? `Request failed (${response.status})`, response.status);
  }

  return response.json() as Promise<T>;
}
