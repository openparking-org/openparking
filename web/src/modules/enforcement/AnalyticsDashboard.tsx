import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { ResponsiveContainer, LineChart, Line, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip } from 'recharts';
import { SettingsContext } from '../../App';
import { analyticsService, type AnalyticsSummary, type DailyRevenue, type ZoneOccupancy, type WeeklyViolation, type WorkflowsSummary } from '../../services/analyticsService';
import { Notice, PageHeading } from '../../components/PageTools';
import { messageOf } from '../../services/adminService';

export function AnalyticsDashboard() {
  const { settings } = useContext(SettingsContext);
  const [days, setDays] = useState(30);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [data, setData] = useState<{ summary: AnalyticsSummary; revenue: DailyRevenue[]; occupancy: ZoneOccupancy[]; violations: WeeklyViolation[]; workflows: WorkflowsSummary } | null>(null);
  const [updated, setUpdated] = useState('');
  const requestId = useRef(0);
  const load = useCallback(async () => {
    const id = ++requestId.current;
    setLoading(true); setError('');
    try {
      const from = new Date(Date.now() - days * 86400000).toISOString();
      const to = new Date().toISOString();
      const [summary, revenue, occupancy, violations, workflows] = await Promise.all([
        analyticsService.getSummary(days, from, to), analyticsService.getDailyRevenue(days, from, to),
        analyticsService.getOccupancy(), analyticsService.getWeeklyViolations(Math.ceil(days / 7), from, to),
        analyticsService.getWorkflowsSummary(from, to),
      ]);
      if (id === requestId.current) { setData({ summary, revenue, occupancy, violations, workflows }); setUpdated(new Date().toLocaleString()); }
    } catch (err) { if (id === requestId.current) setError(messageOf(err)); } finally { if (id === requestId.current) setLoading(false); }
  }, [days]);
  useEffect(() => { void load(); }, [load]);
  return <><PageHeading title="Parking Analytics" description="Session charges, usage, violations, and workflow outcomes."><select aria-label="Reporting period" value={days} onChange={e => setDays(Number(e.target.value))}>{[7, 30, 90].map(n => <option key={n} value={n}>Last {n} days</option>)}</select><button className="btn btn-secondary" disabled={loading} onClick={() => void load()}>Refresh</button></PageHeading><Notice error={error} />
    {loading ? <p role="status">Loading reports…</p> : !error && data && <><div className="operations-grid"><div className="glass-panel operation-card"><span>Recorded charges</span><strong>{settings.defaultCurrency} {data.summary.totalRevenue.toFixed(2)}</strong><small>Calculated session charges</small></div><div className="glass-panel operation-card"><span>Completed sessions</span><strong>{data.summary.completedSessions}</strong></div><div className="glass-panel operation-card"><span>Overstay sessions</span><strong>{data.summary.overstaySessions}</strong></div><div className="glass-panel operation-card"><span>Average stay</span><strong>{data.summary.avgDurationMinutes.toFixed(0)} min</strong></div></div>
      <div className="report-grid"><section className="glass-panel admin-panel"><h2>Daily recorded charges ({settings.defaultCurrency})</h2>{data.revenue.length ? <ResponsiveContainer width="100%" height={280}><LineChart data={data.revenue}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="date" tickFormatter={value => new Date(value).toLocaleDateString()} /><YAxis /><Tooltip /><Line type="monotone" dataKey="revenue" stroke="#2563eb" strokeWidth={3} /></LineChart></ResponsiveContainer> : <p>No charges in this period.</p>}</section><section className="glass-panel admin-panel"><h2>Current occupancy</h2><p className="muted">Live snapshot, independent of the reporting period.</p>{data.occupancy.length ? <ResponsiveContainer width="100%" height={280}><BarChart data={data.occupancy}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="zoneName" /><YAxis /><Tooltip /><Bar dataKey="occupiedSlots" stackId="bays" fill="#dc2626" /><Bar dataKey="reservedSlots" stackId="bays" fill="#d97706" /><Bar dataKey="availableSlots" stackId="bays" fill="#16a34a" /></BarChart></ResponsiveContainer> : <p>No mapped zones.</p>}</section><section className="glass-panel admin-panel"><h2>Weekly violations</h2>{data.violations.length ? <ResponsiveContainer width="100%" height={260}><BarChart data={data.violations}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="weekLabel" /><YAxis allowDecimals={false} /><Tooltip /><Bar dataKey="count" fill="#7c3aed" /></BarChart></ResponsiveContainer> : <p>No violations in this period.</p>}</section><section className="glass-panel admin-panel"><h2>Workflow outcomes</h2>{Object.entries(data.workflows).map(([key, value]) => <div className="occupancy-row" key={key}><span>{key}</span><strong>{value}</strong></div>)}</section></div><p className="muted">Last refreshed: {updated}. Pay marks a simulated payment; these charts report session charges.</p></>}
  </>;
}
