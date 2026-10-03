import { create } from 'zustand';
import { apiGet, apiPost, ApiError } from '../lib/apiClient';
import { readToken, writeToken, isExpired, type SessionUser } from '../lib/auth';

interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: SessionUser;
}

interface AuthState {
  user: SessionUser | null;
  isRestoring: boolean;
  error: string | null;
  isSubmitting: boolean;
  signIn: (email: string, password: string) => Promise<boolean>;
  signOut: () => void;
  restore: () => Promise<void>;
}

export const useAuthStore = create<AuthState>((set: (partial: Partial<AuthState>) => void) => ({
  user: null,
  isRestoring: true,
  error: null,
  isSubmitting: false,

  /**
   * Re-establishes the session on a page reload. The stored token is confirmed
   * against /api/auth/me rather than trusted on its own, so a token for an
   * account that has since been removed does not present a signed-in shell.
   */
  restore: async () => {
    const token = readToken();

    if (!token || isExpired(token)) {
      writeToken(null);
      set({ user: null, isRestoring: false });
      return;
    }

    try {
      const user = await apiGet<SessionUser>('/api/auth/me');
      set({ user, isRestoring: false });
    } catch {
      writeToken(null);
      set({ user: null, isRestoring: false });
    }
  },

  signIn: async (email: string, password: string) => {
    set({ isSubmitting: true, error: null });
    try {
      const result = await apiPost<AuthResponse>('/api/auth/login', { email, password });
      writeToken(result.accessToken);
      set({ user: result.user, isSubmitting: false, error: null });
      return true;
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Could not reach the server';
      set({ error: message, isSubmitting: false, user: null });
      return false;
    }
  },

  signOut: () => {
    writeToken(null);
    set({ user: null, error: null });
  },
}));
