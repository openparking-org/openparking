import { useCallback, useContext, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { SettingsContext } from '../../App';
import { analyticsService, type AnalyticsSummary, type ZoneOccupancy } from '../../services/analyticsService';
import { getData, messageOf, type Page } from '../../services/adminService';
import { Notice, PageHeading } from '../../components/PageTools';

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
  const cards = data ? [
    { label: 'Available bays', value: data.zones.reduce((n, z) => n + z.availableSlots, 0), to: '/zones' },
    { label: 'Occupied bays', value: data.zones.reduce((n, z) => n + z.occupiedSlots, 0), to: '/bookings' },
    { label: 'Active sessions', value: data.summary.activeSessions, to: '/bookings?status=Active' },
    { label: 'Overstay sessions', value: data.summary.overstaySessions, to: '/ai-enforcement' },
    { label: 'Pending AI decisions', value: data.summary.pendingWorkflowsCount, to: '/ai-enforcement' },
    { label: 'Permits to review', value: data.permits, to: '/permits' },
  ] : [];
  return <><PageHeading title="Parking at a glance" description="Live inventory and the work awaiting your team."><Link className="btn btn-primary" to="/book-parking">Create reservation</Link><button className="btn btn-secondary" disabled={loading} onClick={() => void load()}>Refresh</button></PageHeading><Notice error={error} />
    {loading ? <p role="status">Loading operations…</p> : !error && data && <><div className="operations-grid">{cards.map(card => <Link className="glass-panel operation-card" key={card.label} to={card.to}><span>{card.label}</span><strong>{card.value}</strong><small>View details →</small></Link>)}</div><div className="glass-panel admin-panel"><h2>Recorded parking charges · last 24 hours</h2><p className="large-total">{settings.defaultCurrency} {data.summary.totalRevenue.toFixed(2)}</p><p>Calculated session charges; Pay records a simulated paid status.</p><Link to="/analytics" className="btn btn-secondary">View analytics</Link></div><div className="glass-panel admin-panel"><h2>Zone occupancy</h2>{data.zones.length ? data.zones.map(zone => <div className="occupancy-row" key={zone.zoneId}><Link to={`/slot-mapping?zone=${zone.zoneId}`}>{zone.zoneName}</Link><span>{zone.availableSlots} available · {zone.reservedSlots} reserved · {zone.occupiedSlots} occupied</span><progress max="100" value={zone.occupancyPercentage} aria-label={`${zone.zoneName} occupancy`} /></div>) : <p>No zones yet. <Link to="/zones">Create your first zone.</Link></p>}</div><p className="muted">Last refreshed at {updated}</p></>}
  </>;
}
