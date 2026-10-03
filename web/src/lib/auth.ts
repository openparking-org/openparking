/**
 * Access token storage and claim inspection for the dashboard.
 *
 * The token lives in sessionStorage rather than localStorage so it does not
 * outlive the browser tab: an admin console left open on a shared machine
 * should not still be signed in tomorrow. Neither store defends against XSS,
 * which is why the app keeps its dependency surface small and never injects
 * server HTML.
 */
const TOKEN_KEY = 'openparking.accessToken';

export type Role = 'Driver' | 'ParkingAdmin' | 'SystemAdmin';

export interface SessionUser {
  id: string;
  email: string;
  fullName: string;
  role: Role;
}

export function readToken(): string | null {
  try {
    return sessionStorage.getItem(TOKEN_KEY);
  } catch {
    // Storage throws in private mode in some browsers; treat as signed out.
    return null;
  }
}

export function writeToken(token: string | null): void {
  try {
    if (token === null) sessionStorage.removeItem(TOKEN_KEY);
    else sessionStorage.setItem(TOKEN_KEY, token);
  } catch {
    /* non-fatal: the session simply will not survive a reload */
  }
}

/**
 * Reads the expiry claim without verifying the signature. This is only used to
 * avoid sending a token we already know is stale — the API verifies the
 * signature itself, and nothing here is trusted for an access decision.
 */
export function isExpired(token: string): boolean {
  try {
    const [, payload] = token.split('.');
    const claims = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
    return typeof claims.exp === 'number' && claims.exp * 1000 <= Date.now();
  } catch {
    return true;
  }
}

export function isStaff(role: Role | undefined): boolean {
  return role === 'ParkingAdmin' || role === 'SystemAdmin';
}
