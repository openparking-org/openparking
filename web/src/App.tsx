import React, { useEffect } from 'react';
import { BrowserRouter, NavLink, Navigate, Route, Routes, useNavigate } from 'react-router-dom';
import { CalendarCheck, LayoutDashboard, LogOut, Map, Plus, ShieldCheck, Sliders } from 'lucide-react';
import { AnalyticsDashboard } from './modules/enforcement/AnalyticsDashboard';
import { FloorPlanEditor } from './modules/space-availability/FloorPlanEditor';
import { SystemSettingsPage } from './modules/user-access/SystemSettingsPage';
import { LoginPage } from './modules/user-access/LoginPage';
import { BookingManagementPage } from './modules/booking/BookingManagementPage';
import { NewBookingPage } from './modules/booking/NewBookingPage';
import { useAuthStore } from './store/authStore';
import { setUnauthorizedHandler } from './lib/apiClient';
import { isStaff, type Role } from './lib/auth';

interface NavItem {
  to: string;
  label: string;
  icon: React.ReactNode;
  title: string;
  subtitle: string;
  staffOnly?: boolean;
}

const NAV: NavItem[] = [
  {
    to: '/analytics', label: 'AI & Analytics', icon: <LayoutDashboard size={18} />,
    title: 'Operations & Multi-Agent AI Overview', subtitle: 'Live occupancy, revenue and agent decisions',
    staffOnly: true,
  },
  {
    to: '/blueprint', label: 'Blueprint Editor', icon: <Map size={18} />,
    title: 'Space Availability & Indoor Navigation Editor', subtitle: 'Draw slots and waypoints on a floor plan',
    staffOnly: true,
  },
  {
    to: '/bookings', label: 'Bookings', icon: <CalendarCheck size={18} />,
    title: 'Driver Reservations & Live Sessions', subtitle: 'Reservation lifecycle, fees and receipts',
  },
  {
    to: '/bookings/new', label: 'New Booking', icon: <Plus size={18} />,
    title: 'Reserve a Parking Slot', subtitle: 'Price a window and confirm a reservation',
  },
  {
    to: '/settings', label: 'System Settings', icon: <Sliders size={18} />,
    title: 'Dynamic System Settings & Policy Configuration', subtitle: 'Pricing and enforcement policy, applied without a redeploy',
    staffOnly: true,
  },
];

/** Blocks a route until the session is known, then until the role allows it. */
const RequireAuth: React.FC<{ staffOnly?: boolean; children: React.ReactNode }> = ({ staffOnly, children }) => {
  const { user, isRestoring } = useAuthStore();

  if (isRestoring) return <div style={{ padding: '48px', color: 'var(--text-secondary)' }}>Restoring session…</div>;
  if (!user) return <Navigate to="/login" replace />;
  if (staffOnly && !isStaff(user.role)) return <Navigate to="/bookings" replace />;

  return <>{children}</>;
};

const Shell: React.FC<{ item: NavItem; children: React.ReactNode }> = ({ item, children }) => {
  const { user, signOut } = useAuthStore();
  const visible = NAV.filter((n: NavItem) => !n.staffOnly || isStaff(user?.role as Role));

  return (
    <div className="app-container">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <div className="brand-icon">OP</div>
          <div>
            <div className="brand-title">OpenParking</div>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>Admin Operations</div>
          </div>
        </div>

        <ul className="nav-links">
          {visible.map((n: NavItem) => (
            <li key={n.to}>
              <NavLink to={n.to} end className={({ isActive }: { isActive: boolean }) => `nav-link ${isActive ? 'active' : ''}`}>
                {n.icon}
                <span>{n.label}</span>
              </NavLink>
            </li>
          ))}
        </ul>

        <div style={{ marginTop: 'auto', padding: '16px', borderRadius: 'var(--radius-md)', background: 'rgba(255,255,255,0.02)', border: '1px solid var(--border-color)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
            <ShieldCheck size={16} color="#10b981" />
            <span style={{ fontSize: '0.8rem', fontWeight: 600 }}>{user?.fullName}</span>
          </div>
          <p style={{ fontSize: '0.72rem', color: 'var(--text-secondary)', marginBottom: '10px' }}>
            Signed in as {user?.role}
          </p>
          <button className="btn btn-secondary" onClick={signOut} style={{ width: '100%', justifyContent: 'center', fontSize: '0.78rem' }}>
            <LogOut size={14} />
            Sign out
          </button>
        </div>
      </aside>

      <main className="main-content">
        <header className="page-header">
          <div>
            <h1 className="page-title">{item.title}</h1>
            <p className="page-subtitle">{item.subtitle}</p>
          </div>
        </header>
        {children}
      </main>
    </div>
  );
};

const AppRoutes: React.FC = () => {
  const { user, isRestoring, restore, signOut } = useAuthStore();
  const navigate = useNavigate();

  useEffect(() => {
    // A 401 from any request means the token is gone or stale; drop the
    // session and send the operator back to sign-in rather than leaving a
    // shell that silently fails every fetch.
    setUnauthorizedHandler(() => {
      signOut();
      navigate('/login', { replace: true });
    });
  }, [navigate, signOut]);

  useEffect(() => { restore(); }, [restore]);

  const page = (to: string, element: React.ReactNode) => {
    const item = NAV.find((n: NavItem) => n.to === to)!;
    return (
      <RequireAuth staffOnly={item.staffOnly}>
        <Shell item={item}>{element}</Shell>
      </RequireAuth>
    );
  };

  return (
    <Routes>
      <Route
        path="/login"
        element={isRestoring ? <div className="auth-shell">Restoring session…</div>
          : user ? <Navigate to="/bookings" replace /> : <LoginPage />}
      />
      <Route path="/analytics" element={page('/analytics', <AnalyticsDashboard />)} />
      <Route path="/blueprint" element={page('/blueprint', <FloorPlanEditor />)} />
      <Route path="/bookings" element={page('/bookings', <BookingManagementPage />)} />
      <Route path="/bookings/new" element={page('/bookings/new', <NewBookingPage />)} />
      <Route path="/settings" element={page('/settings', <SystemSettingsPage />)} />
      <Route path="*" element={<Navigate to="/bookings" replace />} />
    </Routes>
  );
};

export const App: React.FC = () => (
  <BrowserRouter>
    <AppRoutes />
  </BrowserRouter>
);

export default App;
