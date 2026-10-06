import { useCallback, useContext, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { SettingsContext } from '../../App';
import { analyticsService, type AnalyticsSummary, type ZoneOccupancy } from '../../services/analyticsService';
import { getData, messageOf, type Page } from '../../services/adminService';
import { EmptyState, Loading, Notice, PageHeading } from '../../components/PageTools';
import { type Tone } from '../../lib/status';
import { ArrowUpRight, Bot, CalendarPlus, Car, CircleDollarSign, Clock, FileCheck, MapPin, RefreshCw, ShieldAlert, SquareParking, type LucideIcon } from 'lucide-react';

export function OperationsDashboard() {
  const { settings } = useContext(SettingsContext);
  const [data, setData] = useState<{ summary: AnalyticsSummary; zones: ZoneOccupancy[]; permits: number } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [updated, setUpdated] = useState('');
  const load = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const [summary, zones, permits] = await Promise.all([analyticsService.getSummary(1), analyticsService.getOccupancy(), getData<Page<unknown>>('/api/admin/permits?status=Pending&pageSize=1')]);
      setData({ summary, zones, permits: permits.totalCount }); setUpdated(new Date().toLocaleTimeString());
    } catch (err) { setError(messageOf(err)); } finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  const cards: Array<{ label: string; value: number; to: string; icon: LucideIcon; tone: Tone; meta: string }> = data ? [
    { label: 'Available bays', value: data.zones.reduce((n, z) => n + z.availableSlots, 0), to: '/zones', icon: SquareParking, tone: 'good', meta: 'Ready to book now' },
    { label: 'Occupied bays', value: data.zones.reduce((n, z) => n + z.occupiedSlots, 0), to: '/bookings', icon: Car, tone: 'neutral', meta: 'Vehicles parked' },
    { label: 'Active sessions', value: data.summary.activeSessions, to: '/bookings?status=Active', icon: Clock, tone: 'info', meta: 'Checked in, not yet out' },
    { label: 'Overstay sessions', value: data.summary.overstaySessions, to: '/ai-enforcement', icon: ShieldAlert, tone: data.summary.overstaySessions ? 'crit' : 'neutral', meta: 'Past their booking window' },
    { label: 'Pending AI decisions', value: data.summary.pendingWorkflowsCount, to: '/ai-enforcement', icon: Bot, tone: data.summary.pendingWorkflowsCount ? 'warn' : 'neutral', meta: 'Awaiting your approval' },
    { label: 'Permits to review', value: data.permits, to: '/permits', icon: FileCheck, tone: data.permits ? 'warn' : 'neutral', meta: 'Disability permits pending' },
  ] : [];
  const pct = (part: number, total: number) => total ? `${(part / total) * 100}%` : '0%';
  return <>
    <PageHeading title="Parking at a glance" description="Live inventory and the work awaiting your team.">
      <button className="btn btn-secondary" disabled={loading} onClick={() => void load()}><RefreshCw size={16} className={loading ? 'spin' : ''} aria-hidden="true" />Refresh</button>
      <Link className="btn btn-primary" to="/book-parking"><CalendarPlus size={16} aria-hidden="true" />New reservation</Link>
    </PageHeading>
    <Notice error={error} />
    {loading && !data ? <Loading label="Loading operations…" /> : !error && data && <>
      <div className="kpi-grid cols-3">
        {cards.map(({ label, value, to, icon: Icon, tone, meta }) => <Link className="card kpi" key={label} to={to}>
          <div className="kpi-top"><span className={`kpi-icon ${tone === 'neutral' ? '' : `tone-${tone}`}`}><Icon size={17} aria-hidden="true" /></span><ArrowUpRight size={16} className="kpi-go" aria-hidden="true" /></div>
          <span className="kpi-label">{label}</span>
          <strong className="kpi-value">{value}</strong>
          <span className="kpi-meta">{meta}</span>
        </Link>)}
      </div>
      <div className="grid-split">
        <section className="card">
          <div className="card-header"><div><h2>Zone occupancy</h2><p>Live bay status by parking zone</p></div>
            <div className="legend"><span><i className="dot good" />Available</span><span><i className="dot warn" />Reserved</span><span><i className="dot crit" />Occupied</span></div></div>
          <div className="card-body" style={{ padding: '12px 0 4px' }}>
            {data.zones.length ? data.zones.map(zone => {
              const total = zone.availableSlots + zone.reservedSlots + zone.occupiedSlots;
              return <div className="zone-row" key={zone.zoneId}>
                <div><Link to={`/slot-mapping?zone=${zone.zoneId}`}>{zone.zoneName}</Link><span className="cell-sub">{zone.zoneCode} · {total} mapped bays</span></div>
                <div className="stack-sm">
                  <div className="meter" role="img" aria-label={`${zone.zoneName}: ${zone.availableSlots} available, ${zone.reservedSlots} reserved, ${zone.occupiedSlots} occupied`}>
                    {zone.availableSlots > 0 && <span className="good" style={{ width: pct(zone.availableSlots, total) }} />}
                    {zone.reservedSlots > 0 && <span className="warn" style={{ width: pct(zone.reservedSlots, total) }} />}
                    {zone.occupiedSlots > 0 && <span className="crit" style={{ width: pct(zone.occupiedSlots, total) }} />}
                  </div>
                  <div className="counts"><span>{zone.availableSlots} available</span><span>{zone.reservedSlots} reserved</span><span>{zone.occupiedSlots} occupied</span></div>
                </div>
                <span className="pct">{Math.round(zone.occupancyPercentage)}%<span className="cell-sub" style={{ fontWeight: 500 }}>occupied</span></span>
              </div>;
            }) : <EmptyState icon={MapPin} title="No zones yet" action={<Link className="btn btn-primary" to="/zones">Create a zone</Link>}>Create your first parking zone to start tracking occupancy.</EmptyState>}
          </div>
        </section>
        <section className="card">
          <div className="card-header"><div><h2>Recorded charges</h2><p>Session charges in the last 24 hours</p></div><span className="kpi-icon"><CircleDollarSign size={17} aria-hidden="true" /></span></div>
          <div className="card-body">
            <p className="hero-figure"><small>{settings.defaultCurrency}</small>{data.summary.totalRevenue.toFixed(2)}</p>
            <p className="subtle" style={{ marginTop: 8 }}>Calculated session charges. “Record payment” marks a simulated paid status.</p>
          </div>
          <div className="card-footer"><Link to="/analytics" className="btn btn-secondary btn-sm">View analytics<ArrowUpRight size={14} aria-hidden="true" /></Link><span className="subtle" style={{ marginLeft: 'auto' }}>Updated {updated}</span></div>
        </section>
      </div>
    </>}
  </>;
}
