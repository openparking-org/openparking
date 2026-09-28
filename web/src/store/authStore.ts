import { create, StateCreator } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  role: 'Driver' | 'ParkingAdmin' | 'SystemAdmin';
  hasDisabilityPermit: boolean;
}

interface AuthState {
  token: string | null;
  user: AuthUser | null;
  setAuth: (token: string, user: AuthUser) => void;
  logout: () => void;
  isAuthenticated: () => boolean;
}

type AuthStore = AuthState;

const authStoreCreator: StateCreator<AuthStore> = (set, get) => ({
  token: null,
  user: null,
  setAuth: (token: string, user: AuthUser) => set({ token, user }),
  logout: () => set({ token: null, user: null }),
  isAuthenticated: () => !!get().token,
});

export const useAuthStore = create<AuthStore>()(
  persist(authStoreCreator, { 
    name: 'openparking-auth',
    storage: createJSONStorage(() => localStorage),
  })
);
