import { useEffect, useState } from 'react';
import { Moon, Sun } from 'lucide-react';
import { resolvedTheme, useThemeStore } from '../store/themeStore';

export function ThemeToggle({ className = 'icon-btn' }: { className?: string }) {
  const { mode, setMode } = useThemeStore();
  const [current, setCurrent] = useState(() => resolvedTheme(mode));
  useEffect(() => {
    setCurrent(resolvedTheme(mode));
    if (mode !== 'system' || !window.matchMedia) return;
    const query = window.matchMedia('(prefers-color-scheme: dark)');
    const follow = () => setCurrent(query.matches ? 'dark' : 'light');
    query.addEventListener('change', follow);
    return () => query.removeEventListener('change', follow);
  }, [mode]);
  const next = current === 'dark' ? 'light' : 'dark';
  return <button type="button" className={className} onClick={() => setMode(next)} aria-label={`Switch to ${next} mode`} title={`Switch to ${next} mode`}>
    {current === 'dark' ? <Sun size={18} aria-hidden="true" /> : <Moon size={18} aria-hidden="true" />}
  </button>;
}
