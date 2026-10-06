import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { ResponsiveContainer, AreaChart, Area, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip } from 'recharts';
import { SettingsContext } from '../../App';
import { analyticsService, type AnalyticsSummary, type DailyRevenue, type ZoneOccupancy, type WeeklyViolation, type WorkflowsSummary } from '../../services/analyticsService';
import { EmptyState, Loading, Notice, PageHeading } from '../../components/PageTools';
import { humanize } from '../../lib/status';
import { BarChart3, CircleDollarSign, Clock, RefreshCw, ShieldAlert, SquareParking } from 'lucide-react';
import { messageOf } from '../../services/adminService';

interface TooltipEntry { name?: string | number; value?: number | string; color?: string; dataKey?: string | number }
function ChartTooltip({ active, payload, label, format, labelFormat }: { active?: boolean; payload?: TooltipEntry[]; label?: string | number; format?: (value: number) => string; labelFormat?: (label: string) => string }) {
  if (!active || !payload?.length) return null;
  return <div className="chart-tooltip">
    <strong>{labelFormat ? labelFormat(String(label)) : label}</strong>
    {payload.map(entry => <div key={String(entry.dataKey)}>{payload.length > 1 && <i className="dot" style={{ background: entry.color }} />}{entry.name}<b>{format ? format(Number(entry.value)) : entry.value}</b></div>)}
  </div>;
}
const axis = { stroke: 'var(--chart-grid)', tick: { fill: 'var(--chart-axis)' }, tickLine: false } as const;
const shortDate = (value: string) => new Date(value).toLocaleDateString([], { month: 'short', day: 'numeric' });

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
  const currency = settings.defaultCurrency;
  const outcomes = data ? (['pending', 'running', 'approved', 'autoApproved', 'rejected', 'failed'] as const).map(key => ({ key, value: data.workflows[key] ?? 0 })) : [];
  const maxOutcome = Math.max(1, ...outcomes.map(o => o.value));
  return <>
    <PageHeading title="Parking Analytics" description="Session charges, usage, violations and AI workflow outcomes.">
      <div className="segmented" role="group" aria-label="Reporting period">{[7, 30, 90].map(n => <button key={n} type="button" aria-pressed={days === n} onClick={() => setDays(n)}>{n} days</button>)}</div>
      <button className="btn btn-secondary" disabled={loading} onClick={() => void load()}><RefreshCw size={16} className={loading ? 'spin' : ''} aria-hidden="true" />Refresh</button>
    </PageHeading>
    <Notice error={error} />
    {loading && !data ? <Loading label="Loading reports…" /> : !error && data && <>
      <div className="kpi-grid cols-4">
        <div className="card kpi"><div className="kpi-top"><span className="kpi-icon"><CircleDollarSign size={17} aria-hidden="true" /></span></div><span className="kpi-label">Recorded charges</span><strong className="kpi-value">{data.summary.totalRevenue.toFixed(2)}<small>{currency}</small></strong><span className="kpi-meta">Calculated session charges</span></div>
        <div className="card kpi"><div className="kpi-top"><span className="kpi-icon tone-good"><SquareParking size={17} aria-hidden="true" /></span></div><span className="kpi-label">Completed sessions</span><strong className="kpi-value">{data.summary.completedSessions}</strong><span className="kpi-meta">Checked in and out</span></div>
        <div className="card kpi"><div className="kpi-top"><span className={`kpi-icon ${data.summary.overstaySessions ? 'tone-crit' : ''}`}><ShieldAlert size={17} aria-hidden="true" /></span></div><span className="kpi-label">Overstay sessions</span><strong className="kpi-value">{data.summary.overstaySessions}</strong><span className="kpi-meta">Exceeded booking window</span></div>
        <div className="card kpi"><div className="kpi-top"><span className="kpi-icon tone-info"><Clock size={17} aria-hidden="true" /></span></div><span className="kpi-label">Average stay</span><strong className="kpi-value">{data.summary.avgDurationMinutes.toFixed(0)}<small>min</small></strong><span className="kpi-meta">Per completed session</span></div>
      </div>
      <div className="grid-2">
        <section className="card"><div className="card-header"><div><h2>Daily recorded charges</h2><p>{currency} per day · last {days} days</p></div></div><div className="card-body">
          {data.revenue.length ? <ResponsiveContainer width="100%" height={260}><AreaChart data={data.revenue} margin={{ top: 8, right: 8, left: -12, bottom: 0 }}>
            <defs><linearGradient id="revenue-fill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="var(--ink)" stopOpacity={0.12} /><stop offset="100%" stopColor="var(--ink)" stopOpacity={0} /></linearGradient></defs>
            <CartesianGrid vertical={false} stroke="var(--chart-grid)" />
            <XAxis dataKey="date" {...axis} tickFormatter={shortDate} minTickGap={24} />
            <YAxis {...axis} axisLine={false} width={48} />
            <Tooltip cursor={{ stroke: 'var(--line-strong)' }} content={<ChartTooltip labelFormat={value => new Date(value).toLocaleDateString([], { dateStyle: 'medium' })} format={value => `${currency} ${value.toFixed(2)}`} />} />
            <Area type="monotone" dataKey="revenue" name="Charges" stroke="var(--ink)" strokeWidth={2} fill="url(#revenue-fill)" dot={false} activeDot={{ r: 4, stroke: 'var(--surface)', strokeWidth: 2, fill: 'var(--ink)' }} />
          </AreaChart></ResponsiveContainer> : <EmptyState icon={BarChart3} title="No charges in this period" />}
        </div></section>
        <section className="card"><div className="card-header"><div><h2>Current occupancy</h2><p>Live snapshot, independent of the period</p></div><div className="legend"><span><i className="dot good" />Available</span><span><i className="dot warn" />Reserved</span><span><i className="dot crit" />Occupied</span></div></div><div className="card-body">
          {data.occupancy.length ? <ResponsiveContainer width="100%" height={260}><BarChart data={data.occupancy} margin={{ top: 8, right: 8, left: -12, bottom: 0 }} barCategoryGap="30%">
            <CartesianGrid vertical={false} stroke="var(--chart-grid)" />
            <XAxis dataKey="zoneName" {...axis} />
            <YAxis {...axis} axisLine={false} allowDecimals={false} width={48} />
            <Tooltip cursor={{ fill: 'var(--surface-2)' }} content={<ChartTooltip format={value => `${value} bays`} />} />
            <Bar dataKey="occupiedSlots" name="Occupied" stackId="bays" fill="var(--crit)" stroke="var(--surface)" strokeWidth={2} />
            <Bar dataKey="reservedSlots" name="Reserved" stackId="bays" fill="var(--warn)" stroke="var(--surface)" strokeWidth={2} />
            <Bar dataKey="availableSlots" name="Available" stackId="bays" fill="var(--good)" stroke="var(--surface)" strokeWidth={2} radius={[4, 4, 0, 0]} />
          </BarChart></ResponsiveContainer> : <EmptyState icon={SquareParking} title="No mapped zones" />}
        </div></section>
        <section className="card"><div className="card-header"><div><h2>Weekly violations</h2><p>Overstay penalties issued per week</p></div></div><div className="card-body">
          {data.violations.length ? <ResponsiveContainer width="100%" height={240}><BarChart data={data.violations} margin={{ top: 8, right: 8, left: -12, bottom: 0 }} barCategoryGap="35%">
            <CartesianGrid vertical={false} stroke="var(--chart-grid)" />
            <XAxis dataKey="weekLabel" {...axis} />
            <YAxis {...axis} axisLine={false} allowDecimals={false} width={48} />
            <Tooltip cursor={{ fill: 'var(--surface-2)' }} content={<ChartTooltip format={value => `${value}`} />} />
            <Bar dataKey="count" name="Violations" fill="var(--ink)" radius={[4, 4, 0, 0]} />
          </BarChart></ResponsiveContainer> : <EmptyState icon={ShieldAlert} title="No violations in this period" />}
        </div></section>
        <section className="card"><div className="card-header"><div><h2>AI workflow outcomes</h2><p>{data.workflows.total} workflows in this period</p></div></div><div className="card-body">
          <div className="bar-list">{outcomes.map(o => <div className="bar-list-row" key={o.key}><span>{humanize(o.key)}</span><span className="track"><span className="fill" style={{ display: 'block', width: `${(o.value / maxOutcome) * 100}%` }} /></span><b>{o.value}</b></div>)}</div>
        </div></section>
      </div>
      <p className="subtle" style={{ marginTop: 16 }}>Last refreshed {updated}. “Record payment” marks a simulated payment; these charts report session charges.</p>
    </>}
  </>;
}
