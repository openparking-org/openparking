import { useState, useEffect, createContext, useContext } from 'react';
import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import { SlotMappingEngine } from './modules/space-availability/SlotMappingEngine';
import { useAuthStore } from './store/authStore';
import { ProtectedRoute } from './components/ProtectedRoute';
import { LoginPage } from './modules/user-access/LoginPage';
import { PermitsPage } from './modules/user-access/PermitsPage';
import { PenaltyApprovals } from './modules/enforcement/PenaltyApprovals';
import { DriverBookingLayout } from './modules/booking/DriverBookingLayout';

// --- Types & Context ---

interface Settings {
  defaultCurrency: string;
  aiTolerance: number;
  baseFine: number;
}

interface SettingsContextType {
  settings: Settings;
  updateSettings: (newSettings: Partial<Settings>) => void;
}

// eslint-disable-next-line react-refresh/only-export-components
export const SettingsContext = createContext<SettingsContextType>({
  settings: { defaultCurrency: 'USD', aiTolerance: 85, baseFine: 50 },
  updateSettings: () => {},
});

// --- Mock API Services ---

const api = {
  getSettings: async (): Promise<Settings> => {
    return new Promise(resolve => setTimeout(() => resolve({ defaultCurrency: 'LKR', aiTolerance: 85, baseFine: 50 }), 500));
  },
  getAnalytics: async (): Promise<any> => {
    return new Promise(resolve => setTimeout(() => resolve({
      monthlyPrediction: 4500000,
      peakTime: "14:00 - 16:00 (Weekdays)",
      complianceRate: 92.4
    }), 500));
  },
  getDashboardStats: async (): Promise<any> => {
    return new Promise(resolve => setTimeout(() => resolve({
      totalRevenue: 148920,
      activeOverstays: 142,
      occupancy: 87.4
    }), 500));
  },
  getPenalties: async (): Promise<any[]> => {
    return new Promise(resolve => setTimeout(() => resolve([
      { id: 'INC-9012', vehicle: 'Tesla Model Y', plate: 'CA · 8XYZ421', zone: 'Downtown Core / Bay 12', duration: '2h 15m', aiScore: 98, amount: 150.00, status: 'Critical' },
      { id: 'INC-9013', vehicle: 'Ford F-150', plate: 'TX · PK9021', zone: 'Harbor Wharf / Bay 44', duration: '45m', aiScore: 92, amount: 50.00, status: 'Pending' },
    ]), 500));
  },
  getZones: async (): Promise<any[]> => {
    return new Promise(resolve => setTimeout(() => resolve([
      { id: 1, name: 'Downtown Core', total: 1200, occupied: 1128, status: 'High Congestion' },
      { id: 2, name: 'Financial District', total: 850, occupied: 756, status: 'Normal' },
      { id: 3, name: 'Harbor Wharf', total: 400, occupied: 248, status: 'Normal' },
    ]), 500));
  },
  getLogs: async (): Promise<any[]> => {
    return new Promise(resolve => setTimeout(() => resolve([
      { id: 1, time: '15:24:01', message: 'Sensor DT-404: Status OK (Battery 84%)' },
      { id: 2, time: '15:23:59', message: 'Camera ANPR-12 (Harbor): Plate Scanned [CA 8XYZ421]' },
      { id: 3, time: '15:22:14', message: 'Routing Engine: Mapbox token validated.' },
    ]), 500));
  }
};

// --- Layout ---

function Layout({ children }: { children: React.ReactNode }) {
  const { settings } = useContext(SettingsContext);
  const { user, logout } = useAuthStore();
  
  return (
    <div style={{ display: 'flex', minHeight: '100vh', fontFamily: 'sans-serif' }}>
      <nav style={{ width: '250px', borderRight: '1px solid #ccc', padding: '20px' }}>
        <h2>OpenParking</h2>
        <ul style={{ listStyle: 'none', padding: 0, display: 'flex', flexDirection: 'column', gap: '10px' }}>
          <li><Link to="/">Dashboard</Link></li>
          <li><Link to="/zones">Zones</Link></li>
          <li><Link to="/ai-enforcement">AI Enforcement</Link></li>
          <li><Link to="/analytics">Analytics</Link></li>
          <li><Link to="/hardware">Hardware Logs</Link></li>
          <li><Link to="/permits">Permits Review</Link></li>
          <li><Link to="/settings">Settings</Link></li>
          <li><Link to="/slot-mapping">Slot Mapping</Link></li>
          <li><Link to="/book-parking">🅿️ Book Parking</Link></li>
        </ul>
        <hr />
        <p>Global Currency: <strong>{settings.defaultCurrency}</strong></p>
        
        {user && (
          <div style={{ marginTop: 'auto', paddingTop: '20px' }}>
            <hr />
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <div style={{ fontSize: '0.9rem' }}>
                <strong>{user.fullName}</strong>
                <br />
                <span style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>{user.role}</span>
              </div>
              <button 
                onClick={logout}
                style={{ 
                  background: 'rgba(239, 68, 68, 0.2)', 
                  border: '1px solid rgba(239, 68, 68, 0.4)',
                  color: '#fca5a5',
                  padding: '6px 12px',
                  borderRadius: '6px',
                  cursor: 'pointer',
                  fontSize: '0.85rem'
                }}
              >
                Sign Out
              </button>
            </div>
          </div>
        )}
      </nav>
      <main style={{ flex: 1, padding: '20px' }}>
        {children}
      </main>
    </div>
  );
}

// --- Pages ---

function Dashboard() {
  const { settings } = useContext(SettingsContext);
  const [stats, setStats] = useState<any>(null);
  const [penalties, setPenalties] = useState<any[]>([]);

  useEffect(() => {
    api.getDashboardStats().then(setStats);
    api.getPenalties().then(setPenalties);
  }, []);

  if (!stats) return <p>Loading dashboard...</p>;

  return (
    <div>
      <h1>Dashboard (Overview)</h1>
      <hr />
      <h2>System Status</h2>
      <ul>
        <li><strong>Total Revenue:</strong> {settings.defaultCurrency} {stats.totalRevenue.toFixed(2)}</li>
        <li><strong>Active Overstays:</strong> {stats.activeOverstays} Vehicles</li>
        <li><strong>Occupancy:</strong> {stats.occupancy}%</li>
      </ul>
      <hr />
      <h2>Quick Penalties Overview</h2>
      <table border={1} cellPadding={8} style={{ borderCollapse: 'collapse', width: '100%', textAlign: 'left' }}>
        <thead>
          <tr>
            <th>Incident ID</th>
            <th>Vehicle & Plate</th>
            <th>Status</th>
            <th>Fine Amount</th>
          </tr>
        </thead>
        <tbody>
          {penalties.map(p => (
            <tr key={p.id}>
              <td>{p.id}</td>
              <td>{p.vehicle} ({p.plate})</td>
              <td>{p.status}</td>
              <td>{settings.defaultCurrency} {p.amount.toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Zones() {
  const [zones, setZones] = useState<any[]>([]);
  useEffect(() => {
    api.getZones().then(setZones);
  }, []);

  return (
    <div>
      <h1>Parking Zones</h1>
      <hr />
      <table border={1} cellPadding={8} style={{ borderCollapse: 'collapse', width: '100%', textAlign: 'left' }}>
        <thead>
          <tr>
            <th>Zone Name</th>
            <th>Total Bays</th>
            <th>Occupied</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {zones.map(z => (
            <tr key={z.id}>
              <td>{z.name}</td>
              <td>{z.total}</td>
              <td>{z.occupied} ({(z.occupied / z.total * 100).toFixed(0)}%)</td>
              <td>{z.status}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}



function Analytics() {
  const { settings } = useContext(SettingsContext);
  const [data, setData] = useState<any>(null);

  useEffect(() => {
    api.getAnalytics().then(setData);
  }, []);

  if (!data) return <p>Loading analytics...</p>;

  return (
    <div>
      <h1>Analytics & Reporting</h1>
      <hr />
      <ul>
        <li>Monthly Revenue Prediction: {settings.defaultCurrency} {data.monthlyPrediction.toFixed(2)}</li>
        <li>Peak Violation Time: {data.peakTime}</li>
        <li>Average Compliance Rate: {data.complianceRate}%</li>
      </ul>
    </div>
  );
}

function HardwareLogs() {
  const [logs, setLogs] = useState<any[]>([]);
  useEffect(() => {
    api.getLogs().then(setLogs);
  }, []);

  return (
    <div>
      <h1>Hardware & Sensor Logs</h1>
      <hr />
      <ul>
        {logs.map(log => (
          <li key={log.id}>[{log.time}] {log.message}</li>
        ))}
      </ul>
    </div>
  );
}

function Settings() {
  const { settings, updateSettings } = useContext(SettingsContext);
  const [formData, setFormData] = useState(settings);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    updateSettings(formData);
    alert("Settings saved successfully!");
  };

  return (
    <div>
      <h1>System Settings</h1>
      <hr />
      <form onSubmit={handleSubmit}>
        <div>
          <label>Default Currency Symbol: </label>
          <select 
            value={formData.defaultCurrency} 
            onChange={e => setFormData({...formData, defaultCurrency: e.target.value})}
          >
            <option value="USD">USD ($)</option>
            <option value="EUR">EUR (€)</option>
            <option value="GBP">GBP (£)</option>
            <option value="LKR">LKR (Rs)</option>
          </select>
        </div>
        <br />
        <div>
          <label>AI Minimum Confidence Threshold (%): </label>
          <input 
            type="number" 
            value={formData.aiTolerance}
            onChange={e => setFormData({...formData, aiTolerance: parseInt(e.target.value)})} 
          />
        </div>
        <br />
        <div>
          <label>Default Overstay Fine Amount: </label>
          <input 
            type="number" 
            value={formData.baseFine}
            onChange={e => setFormData({...formData, baseFine: parseInt(e.target.value)})} 
          />
        </div>
        <br />
        <button type="submit">Save Settings</button>
      </form>
    </div>
  );
}

function SlotMapping() {
  return <SlotMappingEngine />;
}

// --- App Root ---

export default function App() {
  const [settings, setSettings] = useState<Settings>({ defaultCurrency: 'USD', aiTolerance: 85, baseFine: 50 });

  useEffect(() => {
    api.getSettings().then(setSettings);
  }, []);

  const updateSettings = (newSettings: Partial<Settings>) => {
    setSettings(prev => ({ ...prev, ...newSettings }));
  };

  return (
    <SettingsContext.Provider value={{ settings, updateSettings }}>
      <Router>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          
          {/* Protected Admin Routes */}
          <Route path="/*" element={
            <ProtectedRoute roles={['ParkingAdmin', 'SystemAdmin']}>
              <Layout>
                <Routes>
                  <Route path="/" element={<Dashboard />} />
                  <Route path="/zones" element={<Zones />} />
                  <Route path="/ai-enforcement" element={<PenaltyApprovals />} />
                  <Route path="/analytics" element={<Analytics />} />
                  <Route path="/hardware" element={<HardwareLogs />} />
                  <Route path="/permits" element={<PermitsPage />} />
                  <Route path="/settings" element={
                    <ProtectedRoute roles={['SystemAdmin']}>
                      <Settings />
                    </ProtectedRoute>
                  } />
                  <Route path="/slot-mapping" element={<SlotMapping />} />
                  <Route path="/book-parking" element={<DriverBookingLayout />} />
                </Routes>
              </Layout>
            </ProtectedRoute>
          } />
        </Routes>
      </Router>
    </SettingsContext.Provider>
  );
}
