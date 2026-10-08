import { useState, useEffect, useCallback, createContext, useContext, lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import { Compass, Loader2 } from 'lucide-react';
const AnalyticsDashboard = lazy(() => import('./modules/enforcement/AnalyticsDashboard').then(module => ({ default: module.AnalyticsDashboard })));
import { OperationsDashboard } from './modules/enforcement/OperationsDashboard';
import { AdminLayout } from './components/AdminLayout';
import { ProtectedRoute } from './components/ProtectedRoute';
import { LoginPage } from './modules/user-access/LoginPage';
import { PermitReviewPage } from './modules/user-access/PermitReviewPage';
import { EnforcementPage } from './modules/enforcement/EnforcementPage';
import { CreateReservationPage } from './modules/booking/CreateReservationPage';
import { GatePage } from './modules/booking/GatePage';
import { ReservationsPage } from './modules/booking/ReservationsPage';
import { ZonesPage } from './modules/space-availability/ZonesPage';
import { MappingPage } from './modules/space-availability/MappingPage';
import { HardwarePage } from './modules/enforcement/HardwarePage';
import { SettingsPage } from './modules/user-access/SettingsPage';
import { UsersPage } from './modules/user-access/UsersPage';
import { useAuthStore } from './store/authStore';
import { ConfirmProvider } from './components/ConfirmDialog';
import { EmptyState, Loading } from './components/PageTools';
import { getData, messageOf, type Setting } from './services/adminService';

interface Settings { defaultCurrency: string; aiTolerance: number; baseFine: number }
// eslint-disable-next-line react-refresh/only-export-components
export const SettingsContext = createContext<{ settings: Settings; updateSettings: (value: Partial<Settings>) => void }>({
  settings: { defaultCurrency: 'USD', aiTolerance: 90, baseFine: 50 }, updateSettings: () => {},
});

function Layout({ children }: { children: React.ReactNode }) {
  const { settings, updateSettings } = useContext(SettingsContext);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setError('');
    getData<Record<string, Setting[]>>('/api/settings', controller.signal).then(grouped => {
      if (controller.signal.aborted) return;
      const values = Object.fromEntries(Object.values(grouped).flat().map(row => [row.key, row.value]));
      updateSettings({ defaultCurrency: values['pricing.default_currency'] || 'USD', aiTolerance: Number(values['permits.auto_approve_confidence'] || '.9') * 100, baseFine: Number(values['overstay.penalty_fixed_amount'] || '50') });
    }).catch(err => { if (!controller.signal.aborted) setError(messageOf(err)); });
    return () => controller.abort();
  }, [revision, updateSettings]);
  return <AdminLayout currency={settings.defaultCurrency}>{error && <div className="notice tone-crit" role="alert"><div>Unable to load shared settings: {error}</div><button className="btn btn-secondary btn-sm" onClick={() => setRevision(n => n + 1)}>Retry</button></div>}{children}</AdminLayout>;
}

function SessionGate({ children }: { children: React.ReactNode }) {
  const token = useAuthStore(state => state.token);
  const setAuth = useAuthStore(state => state.setAuth);
  const [checkedToken, setCheckedToken] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    if (!token) return;
    const controller = new AbortController();
    setError('');
    getData<import('./store/authStore').AuthUser>('/api/users/me', controller.signal).then(user => {
      if (!controller.signal.aborted) { setAuth(token, user); setCheckedToken(token); }
    }).catch(err => { if (!controller.signal.aborted) setError(messageOf(err)); });
    return () => controller.abort();
  }, [token, setAuth, revision]);
  if (token && checkedToken !== token) return <div className="auth-main" style={{ minHeight: '100vh' }}><div className="auth-card" style={{ textAlign: 'center' }}>
    {error ? <><div className="notice tone-crit" role="alert"><div>{error}</div></div><div className="row" style={{ justifyContent: 'center' }}><button className="btn btn-primary" onClick={() => setRevision(n => n + 1)}>Retry</button><button className="btn btn-secondary" onClick={() => useAuthStore.getState().logout()}>Sign out</button></div></>
      : <p className="row muted" role="status" style={{ justifyContent: 'center' }}><Loader2 size={18} className="spin" aria-hidden="true" />Checking your admin session…</p>}
  </div></div>;
  return <>{children}</>;
}

export default function App() {
  const [settings, setSettings] = useState<Settings>({ defaultCurrency: 'USD', aiTolerance: 90, baseFine: 50 });
  const updateSettings = useCallback((value: Partial<Settings>) => setSettings(previous => ({ ...previous, ...value })), []);
  return <SettingsContext.Provider value={{ settings, updateSettings }}><ConfirmProvider><BrowserRouter><Routes>
    <Route path="/login" element={<LoginPage />} />
    
    <Route element={<SessionGate><ProtectedRoute /></SessionGate>}>
      <Route path="/book-parking" element={<CreateReservationPage />} />
      <Route path="/gate" element={<GatePage />} />
      <Route path="/bookings" element={<ReservationsPage />} />
    </Route>

    <Route path="/*" element={<SessionGate><ProtectedRoute roles={['ParkingAdmin', 'SystemAdmin']}><Layout><Routes>
      <Route path="/" element={<OperationsDashboard />} />
      <Route path="/zones" element={<ZonesPage />} />
      <Route path="/ai-enforcement" element={<EnforcementPage />} />
      <Route path="/analytics" element={<Suspense fallback={<Loading label="Loading analytics…" />}><AnalyticsDashboard /></Suspense>} />
      <Route path="/hardware" element={<HardwarePage />} />
      <Route path="/permits" element={<PermitReviewPage />} />
      <Route path="/settings" element={<ProtectedRoute roles={['SystemAdmin']}><SettingsPage /></ProtectedRoute>} />
      <Route path="/users" element={<ProtectedRoute roles={['SystemAdmin']}><UsersPage /></ProtectedRoute>} />
      <Route path="/slot-mapping" element={<MappingPage />} />
      <Route path="*" element={<div className="card"><EmptyState icon={Compass} title="Page not found" action={<Link className="btn btn-primary" to="/">Return to dashboard</Link>}>This page doesn’t exist or has moved.</EmptyState></div>} />
    </Routes></Layout></ProtectedRoute></SessionGate>} />
  </Routes></BrowserRouter></ConfirmProvider></SettingsContext.Provider>;
}
