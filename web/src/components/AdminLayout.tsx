import { useState, type ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { LayoutDashboard, MapPin, ShieldCheck, BarChart3, Radio, FileCheck, Settings, Layers, Car, LogOut, Menu, X, ChevronRight, Calendar, Users } from 'lucide-react';
import { useAuthStore } from '../store/authStore';

const navigation = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, group: 'Operations' },
  { to: '/zones', label: 'Parking Zones', icon: MapPin, group: 'Operations' },
  { to: '/bookings', label: 'Reservations', icon: Calendar, group: 'Operations' },
  { to: '/ai-enforcement', label: 'AI Enforcement', icon: ShieldCheck, group: 'Operations' },
  { to: '/analytics', label: 'Analytics', icon: BarChart3, group: 'Operations' },
  { to: '/hardware', label: 'Hardware Logs', icon: Radio, group: 'Operations' },
  { to: '/permits', label: 'Permits Review', icon: FileCheck, group: 'Management' },
  { to: '/settings', label: 'Settings', icon: Settings, group: 'Management' },
  { to: '/slot-mapping', label: 'Slot Mapping', icon: Layers, group: 'Management' },
  { to: '/book-parking', label: 'Create Reservation', icon: Car, group: 'Management' },
  { to: '/users', label: 'Users', icon: Users, group: 'Management' },
];

export function AdminLayout({ children, currency }: { children: ReactNode; currency: string }) {
  const { user, logout } = useAuthStore();
  const { pathname } = useLocation();
  const [open, setOpen] = useState(false);
  const title = navigation.find(item => item.to === pathname)?.label || 'Workspace';
  const initials = user?.fullName.split(' ').filter(Boolean).map(part => part[0]).slice(0, 2).join('') || 'OP';
  return (
    <div className="admin-shell">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <aside className={`admin-sidebar ${open ? 'is-open' : ''}`} id="admin-navigation" onKeyDown={event => { if (event.key === 'Escape') setOpen(false); }}>
        <NavLink to="/" className="admin-brand" onClick={() => setOpen(false)}>
          <span className="parking-mark">P</span><span>OpenParking<small>ADMIN WORKSPACE</small></span>
        </NavLink>
        <nav aria-label="Main navigation">
          {['Operations', 'Management'].map(group => (
            <div className="nav-group" key={group}>
              <p className="nav-caption">{group}</p>
              {navigation.filter(item => item.group === group && (!['/settings', '/users'].includes(item.to) || user?.role === 'SystemAdmin')).map(({ to, label, icon: Icon }) => (
                <NavLink key={to} to={to} end={to === '/'} className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`} onClick={() => setOpen(false)}>
                  <Icon size={18} aria-hidden="true" /><span>{label}</span>
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
        {user && <div className="sidebar-account">
          <span className="account-avatar">{initials}</span>
          <div><strong>{user.fullName}</strong><small>{user.role}</small></div>
          <button onClick={logout} className="sign-out" aria-label="Sign out" title="Sign out"><LogOut size={18} /></button>
        </div>}
      </aside>
      {open && <button className="sidebar-scrim" aria-label="Close navigation" onClick={() => setOpen(false)} />}
      <div className="admin-workspace">
        <header className="workspace-header">
          <button className="menu-toggle" aria-label={open ? 'Close navigation' : 'Open navigation'} aria-expanded={open} aria-controls="admin-navigation" onClick={() => setOpen(!open)}>{open ? <X size={20} /> : <Menu size={20} />}</button>
          <div className="workspace-breadcrumb"><span>Workspace</span><ChevronRight size={14} /><strong>{title}</strong></div>
          <div className="workspace-profile"><span className="currency-chip">{currency}</span><span className="header-avatar" title={user?.fullName}>{initials}</span></div>
        </header>
        <main id="main-content" className="workspace-content" tabIndex={-1}>{children}</main>
        <footer className="workspace-footer">OpenParking<span>Parking management, in one place.</span></footer>
      </div>
    </div>
  );
}
