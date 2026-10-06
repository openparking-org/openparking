import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAdminPage } from '../../hooks/useAdminPage';
import { useSlotUpdates } from '../../hooks/useSlotUpdates';
import { allZones, getData, postData, messageOf, type Driver, type Slot, type Zone } from '../../services/adminService';
import { EmptyState, Loading, Notice, PageHeading } from '../../components/PageTools';
import { CalendarDays, CheckCircle2, Layers, RefreshCw } from 'lucide-react';

function localTimeAfter(minutes: number) {
  const date = new Date(Date.now() + minutes * 60000);
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}

export function CreateReservationPage() {
  const [zones, setZones] = useState<Zone[]>([]);
  const [zoneId, setZoneId] = useState('');
  const [slots, setSlots] = useState<Slot[]>([]);
  const [floor, setFloor] = useState(0);
  const [slotId, setSlotId] = useState('');
  const drivers = useAdminPage<Driver>('/api/admin/drivers');
  const [driver, setDriver] = useState<Driver | null>(null);
  const [plate, setPlate] = useState('');
  const [start, setStart] = useState(() => localTimeAfter(5));
  const [end, setEnd] = useState(() => localTimeAfter(125));
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<{ id: string; estimatedFee: number } | null>(null);
  const { updates, isConnected, connectionRevision } = useSlotUpdates(zoneId);
  useEffect(() => {
    const controller = new AbortController();
    allZones(controller.signal).then(items => { setZones(items); setZoneId(items[0]?.id || ''); }).catch(err => { if (!controller.signal.aborted) setError(messageOf(err)); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, []);
  const loadSlots = useCallback(async (signal?: AbortSignal) => {
    if (!zoneId) return;
    setLoading(true); setSlotId(''); setError('');
    try {
      const detail = await getData<{ slots: Slot[] }>(`/api/zones/${zoneId}`, signal);
      if (!signal?.aborted) {
        setSlots(detail.slots);
        const floors = [...new Set(detail.slots.map(slot => slot.floor))].sort((a, b) => a - b);
        setFloor(current => floors.includes(current) ? current : floors[0] || 0);
      }
    }
    catch (err) { if (!signal?.aborted) setError(messageOf(err)); }
    finally { if (!signal?.aborted) setLoading(false); }
  }, [zoneId]);
  useEffect(() => { const controller = new AbortController(); void loadSlots(controller.signal); return () => controller.abort(); }, [loadSlots, connectionRevision]);
  useEffect(() => {
    if (Object.keys(updates).length) {
      setSlots(current => current.map(slot => updates[slot.id] ? { ...slot, status: updates[slot.id].status } : slot));
      if (slotId && updates[slotId] && updates[slotId].status !== 'Available') setSlotId('');
    }
  }, [updates, slotId]);
  const zone = zones.find(item => item.id === zoneId);
  const floors = [...new Set(slots.map(slot => slot.floor))].sort((a, b) => a - b);
  const duration = (new Date(end).getTime() - new Date(start).getTime()) / 3600000;
  const estimate = zone && duration > 0 ? duration * zone.baseHourlyRate : 0;
  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setError('');
    if (!driver || !slotId || !start || !end || duration <= 0 || new Date(start).getTime() < Date.now() - 60000) { setError('Select a driver, available bay, and valid future time window.'); return; }
    setBusy(true);
    try {
      const booking = await postData<{ id: string; estimatedFee: number }>('/api/admin/bookings', { driverId: driver.id, slotId, vehiclePlate: plate.trim().toUpperCase(), startTime: new Date(start).toISOString(), endTime: new Date(end).toISOString() });
      setResult(booking); setSlotId(''); await loadSlots();
    } catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  const selectedSlot = slots.find(slot => slot.id === slotId);
  const floorSlots = slots.filter(slot => slot.floor === floor);
  const available = floorSlots.filter(slot => slot.status === 'Available').length;
  return <>
    <PageHeading title="New Reservation" description="Reserve an available bay on behalf of a driver."><Link className="btn btn-secondary" to="/bookings"><CalendarDays size={16} aria-hidden="true" />All reservations</Link></PageHeading>
    <Notice error={error || drivers.error} />
    {result && <div className="card" role="status" style={{ maxWidth: 560 }}>
      <div className="card-header"><div className="result-head"><span className="result-icon tone-good"><CheckCircle2 size={20} aria-hidden="true" /></span><div><h2>Reservation confirmed</h2><p>The bay is now held for the driver.</p></div></div></div>
      <div className="card-body"><dl className="kv"><dt>Reference</dt><dd className="mono">{result.id}</dd><dt>Server estimate</dt><dd>{zone?.currency} {result.estimatedFee.toFixed(2)}</dd></dl></div>
      <div className="card-footer"><Link className="btn btn-primary" to="/bookings">Manage reservation</Link><button className="btn btn-secondary" onClick={() => setResult(null)}>Create another</button></div>
    </div>}
    {!result && <form onSubmit={submit} className="grid-split">
      <section className="card">
        <div className="card-header"><div><h2>1. Choose a bay</h2><p>{loading ? 'Loading…' : `${available} of ${floorSlots.length} bays available on this floor`}</p></div><span className={`live ${isConnected ? 'on' : ''}`}>{isConnected ? 'Live' : 'Offline'}</span></div>
        <div className="card-body stack">
          <div className="row" style={{ alignItems: 'flex-end' }}>
            <label className="field" style={{ flex: '1 1 220px' }}><span className="field-label">Zone</span><select required value={zoneId} onChange={e => { setZoneId(e.target.value); setFloor(0); setSlotId(''); }}>{zones.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
            <label className="field" style={{ width: 140 }}><span className="field-label">Floor</span><select value={floor} onChange={e => { setFloor(Number(e.target.value)); setSlotId(''); }}>{(floors.length ? floors : [0]).map(f => <option key={f} value={f}>Floor {f}</option>)}</select></label>
            <button type="button" className="btn btn-secondary" onClick={() => void loadSlots()}><RefreshCw size={16} aria-hidden="true" />Refresh</button>
          </div>
          {!isConnected && <p className="subtle">Live updates are disconnected. Refresh to check availability.</p>}
          <div className="legend"><span><i className="dot good" />Available</span><span><i className="dot warn" />Reserved</span><span><i className="dot crit" />Occupied</span><span><i className="dot neutral" />Maintenance</span></div>
          {loading ? <Loading label="Loading bays…" /> : <div className="bay-grid">{floorSlots.map(slot => <button type="button" key={slot.id} aria-pressed={slot.id === slotId} className={`bay ${slot.status.toLowerCase()}`} disabled={slot.status !== 'Available' || (slot.type === 'Accessible' && !driver?.hasDisabilityPermit)} title={slot.type === 'Accessible' && !driver?.hasDisabilityPermit ? 'Requires a driver with a verified disability permit' : undefined} onClick={() => setSlotId(slot.id)}><strong>{slot.slotNumber}</strong><span>{slot.type}</span><small>{slot.status}</small></button>)}</div>}
          {!loading && slots.length === 0 && zones.length > 0 && <EmptyState icon={Layers} title="No mapped bays" action={<Link className="btn btn-secondary" to={`/slot-mapping?zone=${zoneId}`}>Open slot mapping</Link>}>Add bays to this zone before taking reservations.</EmptyState>}
          {!loading && !zones.length && <EmptyState title="No parking zones" action={<Link className="btn btn-primary" to="/zones">Create a zone</Link>}>Create a parking zone first.</EmptyState>}
        </div>
      </section>
      <section className="card" style={{ position: 'sticky', top: 'calc(var(--topbar-h) + 16px)' }}>
        <div className="card-header"><div><h2>2. Driver and time</h2><p>Only existing driver accounts can be booked.</p></div></div>
        <div className="card-body stack">
          <label className="field"><span className="field-label">Find driver</span><input type="search" placeholder="Search name or email" value={drivers.search} onChange={e => drivers.setSearch(e.target.value)} /></label>
          <label className="field"><span className="field-label">Driver</span><select required value={driver?.id || ''} onChange={e => { setDriver(drivers.data?.items.find(item => item.id === e.target.value) || null); setSlotId(''); }}><option value="">{drivers.loading ? 'Loading drivers…' : 'Select a driver'}</option>{driver && !drivers.data?.items.some(item => item.id === driver.id) && <option value={driver.id}>{driver.fullName}</option>}{drivers.data?.items.map(item => <option key={item.id} value={item.id}>{item.fullName} · {item.email}</option>)}</select>{driver?.hasDisabilityPermit && <span className="field-hint">Verified permit: accessible bays are available.</span>}</label>
          <label className="field"><span className="field-label">Vehicle plate</span><input required maxLength={32} placeholder="ABC-1234" style={{ textTransform: 'uppercase' }} value={plate} onChange={e => setPlate(e.target.value)} /></label>
          <div className="form-grid">
            <label className="field"><span className="field-label">Start</span><input type="datetime-local" required value={start} onChange={e => setStart(e.target.value)} /></label>
            <label className="field"><span className="field-label">End</span><input type="datetime-local" required value={end} onChange={e => setEnd(e.target.value)} /></label>
          </div>
          <div>
            <div className="summary-line"><span>Bay</span><strong>{selectedSlot ? `${selectedSlot.slotNumber} · ${selectedSlot.type}` : 'Not selected'}</strong></div>
            <div className="summary-line"><span>Duration</span><strong>{duration > 0 ? `${duration.toFixed(duration % 1 ? 1 : 0)} h` : '—'}</strong></div>
            <div className="summary-line" style={{ borderBottom: 0 }}><span>Base estimate</span><strong>{zone?.currency} {estimate.toFixed(2)}</strong></div>
            <p className="subtle">The final estimate includes server pricing and eligible discounts.</p>
          </div>
        </div>
        <div className="card-footer"><button className="btn btn-primary btn-block btn-lg" disabled={busy || !slotId || !driver}>{busy ? 'Creating…' : 'Confirm reservation'}</button></div>
      </section>
    </form>}
  </>;
}
