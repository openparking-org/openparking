import { useEffect, useState, type ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { LayoutDashboard, MapPin, ShieldCheck, BarChart3, Radio, FileCheck, Settings, Layers, LogOut, Menu, X, ChevronRight, CalendarDays, CalendarPlus, ScanLine, Users } from 'lucide-react';
import { useAuthStore } from '../store/authStore';
import { ThemeToggle } from './ThemeToggle';

const navigation = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, group: 'Overview' },
  { to: '/analytics', label: 'Analytics', icon: BarChart3, group: 'Overview' },
  { to: '/gate', label: 'Gate Entry / Exit', icon: ScanLine, group: 'Operations' },
  { to: '/bookings', label: 'Reservations', icon: CalendarDays, group: 'Operations' },
  { to: '/book-parking', label: 'New Reservation', icon: CalendarPlus, group: 'Operations' },
  { to: '/ai-enforcement', label: 'AI Enforcement', icon: ShieldCheck, group: 'Operations' },
  { to: '/zones', label: 'Parking Zones', icon: MapPin, group: 'Parking setup' },
  { to: '/slot-mapping', label: 'Slot Mapping', icon: Layers, group: 'Parking setup' },
  { to: '/hardware', label: 'Hardware Logs', icon: Radio, group: 'Parking setup' },
  { to: '/permits', label: 'Permit Review', icon: FileCheck, group: 'Administration' },
  { to: '/users', label: 'Users', icon: Users, group: 'Administration', systemAdmin: true },
  { to: '/settings', label: 'Settings', icon: Settings, group: 'Administration', systemAdmin: true },
];
const groups = ['Overview', 'Operations', 'Parking setup', 'Administration'];

export function AdminLayout({ children, currency }: { children: ReactNode; currency: string }) {
  const { user, logout } = useAuthStore();
  const { pathname } = useLocation();
  const [open, setOpen] = useState(false);
  const current = navigation.find(item => item.to === pathname);
  const title = current?.label || 'Workspace';
  const initials = user?.fullName.split(' ').filter(Boolean).map(part => part[0]).slice(0, 2).join('').toUpperCase() || 'OP';
  const items = navigation.filter(item => !item.systemAdmin || user?.role === 'SystemAdmin');
  useEffect(() => { document.title = `${title} · OpenParking Admin`; }, [title]);
  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <aside className={`sidebar ${open ? 'is-open' : ''}`} id="admin-navigation" onKeyDown={event => { if (event.key === 'Escape') setOpen(false); }}>
        <NavLink to="/" className="brand" onClick={() => setOpen(false)}>
          <span className="brand-mark" aria-hidden="true">P</span>
          <span className="brand-name">OpenParking<small>Admin console</small></span>
        </NavLink>
        <nav aria-label="Main navigation">
          {groups.map(group => {
            const links = items.filter(item => item.group === group);
            if (!links.length) return null;
            return <div className="nav-section" key={group}>
              <p className="nav-label">{group}</p>
              {links.map(({ to, label, icon: Icon }) => (
                <NavLink key={to} to={to} end={to === '/'} className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`} onClick={() => setOpen(false)}>
                  <Icon size={18} aria-hidden="true" /><span>{label}</span>
                </NavLink>
              ))}
            </div>;
          })}
        </nav>
        {user && <div className="sidebar-footer">
          <span className="avatar" aria-hidden="true">{initials}</span>
          <div className="who"><strong>{user.fullName}</strong><small>{user.role === 'SystemAdmin' ? 'System admin' : 'Parking admin'}</small></div>
          <button onClick={logout} className="btn btn-ghost btn-icon btn-sm" aria-label="Sign out" title="Sign out"><LogOut size={18} /></button>
        </div>}
      </aside>
      {open && <button className="sidebar-scrim" aria-label="Close navigation" onClick={() => setOpen(false)} />}
      <div className="workspace">
        <header className="topbar">
          <button className="icon-btn menu-toggle" aria-label={open ? 'Close navigation' : 'Open navigation'} aria-expanded={open} aria-controls="admin-navigation" onClick={() => setOpen(!open)}>{open ? <X size={20} /> : <Menu size={20} />}</button>
          <div className="crumbs">{current && <><span>{current.group}</span><ChevronRight size={14} aria-hidden="true" /></>}<strong>{title}</strong></div>
          <div className="topbar-actions">
            <span className="chip" title="System currency">{currency}</span>
            <ThemeToggle />
            <span className="avatar sm" title={user?.fullName} aria-hidden="true">{initials}</span>
          </div>
        </header>
        <main id="main-content" className="content" tabIndex={-1}>{children}</main>
        <footer className="app-footer">OpenParking<span>Parking management, in one place.</span></footer>
      </div>
    </div>
  );
}
