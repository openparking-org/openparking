import React, { useState } from 'react';
import { LayoutDashboard, Map, Sliders, CalendarCheck, ShieldCheck } from 'lucide-react';
import { AnalyticsDashboard } from './modules/enforcement/AnalyticsDashboard';
import { FloorPlanEditor } from './modules/space-availability/FloorPlanEditor';
import { SystemSettingsPage } from './modules/user-access/SystemSettingsPage';
import { BookingManagementPage } from './modules/booking/BookingManagementPage';

export const App: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'analytics' | 'map' | 'settings' | 'bookings'>('analytics');

  return (
    <div className="app-container">
      {/* Sidebar Navigation */}
      <aside className="sidebar">
        <div className="sidebar-brand">
          <div className="brand-icon">OP</div>
          <div>
            <div className="brand-title">OpenParking</div>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>Admin Operations</div>
          </div>
        </div>

        <ul className="nav-links">
          <li>
            <button
              className={`nav-link ${activeTab === 'analytics' ? 'active' : ''}`}
              style={{ width: '100%', background: 'none', border: 'none', textAlign: 'left', cursor: 'pointer' }}
              onClick={() => setActiveTab('analytics')}
            >
              <LayoutDashboard size={18} />
              <span>AI & Analytics</span>
            </button>
          </li>
          <li>
            <button
              className={`nav-link ${activeTab === 'map' ? 'active' : ''}`}
              style={{ width: '100%', background: 'none', border: 'none', textAlign: 'left', cursor: 'pointer' }}
              onClick={() => setActiveTab('map')}
            >
              <Map size={18} />
              <span>Blueprint Editor</span>
            </button>
          </li>
          <li>
            <button
              className={`nav-link ${activeTab === 'bookings' ? 'active' : ''}`}
              style={{ width: '100%', background: 'none', border: 'none', textAlign: 'left', cursor: 'pointer' }}
              onClick={() => setActiveTab('bookings')}
            >
              <CalendarCheck size={18} />
              <span>Bookings</span>
            </button>
          </li>
          <li>
            <button
              className={`nav-link ${activeTab === 'settings' ? 'active' : ''}`}
              style={{ width: '100%', background: 'none', border: 'none', textAlign: 'left', cursor: 'pointer' }}
              onClick={() => setActiveTab('settings')}
            >
              <Sliders size={18} />
              <span>System Settings</span>
            </button>
          </li>
        </ul>

        <div style={{ marginTop: 'auto', padding: '16px', borderRadius: 'var(--radius-md)', background: 'rgba(255,255,255,0.02)', border: '1px solid var(--border-color)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
            <ShieldCheck size={16} color="#10b981" />
            <span style={{ fontSize: '0.8rem', fontWeight: 600 }}>System Live</span>
          </div>
          <p style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
            Oracle ARM VM + Cloudflare Workers Gateway
          </p>
        </div>
      </aside>

      {/* Main Content View */}
      <main className="main-content">
        <header className="page-header">
          <div>
            <h1 className="page-title">
              {activeTab === 'analytics' && 'Operations & Multi-Agent AI Overview'}
              {activeTab === 'map' && 'Space Availability & Indoor Navigation Editor'}
              {activeTab === 'bookings' && 'Driver Reservations & Live Sessions'}
              {activeTab === 'settings' && 'Dynamic System Settings & Policy Configuration'}
            </h1>
            <p className="page-subtitle">
              OpenParking Integrated Full-Stack & Agentic AI Infrastructure
            </p>
          </div>
        </header>

        {activeTab === 'analytics' && <AnalyticsDashboard />}
        {activeTab === 'map' && <FloorPlanEditor />}
        {activeTab === 'bookings' && <BookingManagementPage />}
        {activeTab === 'settings' && <SystemSettingsPage />}
      </main>
    </div>
  );
};

export default App;
