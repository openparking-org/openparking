import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAdminPage } from '../../hooks/useAdminPage';
import { useSlotUpdates } from '../../hooks/useSlotUpdates';
import { allZones, getData, postData, messageOf, type Driver, type Slot, type Zone } from '../../services/adminService';
import { Notice, PageHeading } from '../../components/PageTools';

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
  return <><PageHeading title="Create Reservation" description="Reserve an available bay on behalf of a driver."><Link className="btn btn-secondary" to="/bookings">Reservations</Link></PageHeading><Notice error={error || drivers.error} />
    {result && <div className="glass-panel admin-panel" role="status"><h2>Reservation confirmed</h2><p>Reference: <strong>{result.id}</strong></p><p>Server estimate: {zone?.currency} {result.estimatedFee.toFixed(2)}</p><Link className="btn btn-primary" to="/bookings">Manage reservation</Link><button className="btn btn-secondary" onClick={() => setResult(null)}>Create another</button></div>}
    {!result && <form onSubmit={submit} className="reservation-grid"><section className="glass-panel admin-panel"><h2>Choose a bay</h2><div className="admin-toolbar"><label>Zone<select required value={zoneId} onChange={e => { setZoneId(e.target.value); setFloor(0); setSlotId(''); }}>{zones.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><label>Floor<select value={floor} onChange={e => { setFloor(Number(e.target.value)); setSlotId(''); }}>{(floors.length ? floors : [0]).map(f => <option key={f} value={f}>Floor {f}</option>)}</select></label><button type="button" className="btn btn-secondary" onClick={() => void loadSlots()}>Refresh bays</button></div><p className="muted">{isConnected ? 'Live availability connected' : 'Live updates disconnected. Refresh to check availability.'}</p>{loading ? <p role="status">Loading bays…</p> : <div className="bay-grid">{slots.filter(slot => slot.floor === floor).map(slot => <button type="button" key={slot.id} aria-pressed={slot.id === slotId} className={`bay-button ${slot.status.toLowerCase()} ${slot.id === slotId ? 'selected' : ''}`} disabled={slot.status !== 'Available' || (slot.type === 'Accessible' && !driver?.hasDisabilityPermit)} onClick={() => setSlotId(slot.id)}><strong>{slot.slotNumber}</strong><span>{slot.type}</span><small>{slot.status}</small></button>)}</div>}{!loading && slots.length === 0 && <p>No mapped bays. <Link to={`/slot-mapping?zone=${zoneId}`}>Add bays in Slot Mapping.</Link></p>}{!loading && !zones.length && <Link to="/zones">Create a parking zone first.</Link>}</section>
      <section className="glass-panel admin-panel"><h2>Driver and time window</h2><label>Find driver<input placeholder="Search name or email" value={drivers.search} onChange={e => drivers.setSearch(e.target.value)} /></label><label>Driver<select required value={driver?.id || ''} onChange={e => { setDriver(drivers.data?.items.find(item => item.id === e.target.value) || null); setSlotId(''); }}><option value="">Select an existing driver</option>{driver && !drivers.data?.items.some(item => item.id === driver.id) && <option value={driver.id}>{driver.fullName}</option>}{drivers.data?.items.map(item => <option key={item.id} value={item.id}>{item.fullName} · {item.email}</option>)}</select></label>{drivers.loading && <p>Loading drivers…</p>}<label>Vehicle plate<input required maxLength={32} value={plate} onChange={e => setPlate(e.target.value)} /></label><label>Start time<input type="datetime-local" required value={start} onChange={e => setStart(e.target.value)} /></label><label>End time<input type="datetime-local" required value={end} onChange={e => setEnd(e.target.value)} /></label><p>Selected bay: <strong>{slots.find(slot => slot.id === slotId)?.slotNumber || 'Choose a bay'}</strong></p><p>Base estimate: {zone?.currency} {estimate.toFixed(2)}<small className="muted block">Final reservation estimate includes server pricing and eligible discounts.</small></p><button className="btn btn-primary" disabled={busy || !slotId || !driver}>{busy ? 'Creating…' : 'Confirm reservation'}</button></section>
    </form>}
  </>;
}
