import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

export type ThemeMode = 'system' | 'light' | 'dark';

interface ThemeState {
  mode: ThemeMode;
  setMode: (mode: ThemeMode) => void;
}

function apply(mode: ThemeMode) {
  if (typeof document === 'undefined') return;
  if (mode === 'system') delete document.documentElement.dataset.theme;
  else document.documentElement.dataset.theme = mode;
}

export const useThemeStore = create<ThemeState>()(
  persist(
    set => ({
      mode: 'system',
      setMode: mode => { apply(mode); set({ mode }); },
    }),
    {
      name: 'openparking-theme',
      storage: createJSONStorage(() => localStorage),
      onRehydrateStorage: () => state => apply(state?.mode ?? 'system'),
    },
  ),
);

/** The theme actually on screen, resolving "system" against the OS preference. */
export function resolvedTheme(mode: ThemeMode): 'light' | 'dark' {
  if (mode !== 'system') return mode;
  return typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}
