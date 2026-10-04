import { useEffect, useRef, useState, useContext } from 'react';
import { Link } from 'react-router-dom';
import { SettingsContext } from '../../App';
import { Notice, PageHeading } from '../../components/PageTools';
import { allZones, messageOf, postData, type Zone } from '../../services/adminService';

interface GateSession {
  id: string; bookingId: string; slotNumber: string; zoneName: string;
  vehiclePlate: string; status: string; checkInTime: string; checkOutTime?: string;
  totalFee: number; penaltyFee: number; currency: string;
}

export function GatePage() {
  const [zones, setZones] = useState<Zone[]>([]);
  const [zoneId, setZoneId] = useState('');
  const [plate, setPlate] = useState('');
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false);
  const pending = useRef(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<GateSession | null>(null);
  const [paid, setPaid] = useState(false);
  const { settings } = useContext(SettingsContext);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    allZones(controller.signal).then(rows => {
      if (controller.signal.aborted) return;
      setZones(rows); setZoneId(current => current || rows[0]?.id || '');
    }).catch(err => { if (!controller.signal.aborted) setError(messageOf(err)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [revision]);

  async function operate(action: 'entry' | 'exit') {
    if (pending.current) return;
    const vehiclePlate = plate.trim().toUpperCase();
    if (!zoneId || !/^[A-Z0-9]{3,20}$/.test(vehiclePlate.replace(/[\s-]/g, ''))) {
      setError('Select a zone and enter a valid vehicle number.'); return;
    }
    pending.current = true; setBusy(true); setError(''); setResult(null); setPaid(false);
    try { setResult(await postData<GateSession>(`/api/gate/${action}`, { vehiclePlate, zoneId })); }
    catch (err) { setError(messageOf(err)); }
    finally { pending.current = false; setBusy(false); }
  }

  async function recordPayment() {
    if (!result || pending.current) return;
    pending.current = true; setBusy(true); setError('');
    try { await postData(`/api/admin/bookings/${result.bookingId}/pay`, {}); setPaid(true); }
    catch (err) { setError(messageOf(err)); }
    finally { pending.current = false; setBusy(false); }
  }

  return <>
    <PageHeading title="Gate Entry / Exit" description="Enter the vehicle number to check a customer in or out of their reservation.">
      <Link className="btn btn-secondary" to="/bookings">View reservations</Link>
    </PageHeading>
    <Notice error={error} />
    <div className="glass-panel admin-panel">
      {loading ? <p role="status">Loading parking zones…</p> : <>
        {zones.length === 0 && <p>No zones are available. <button className="btn btn-secondary" onClick={() => setRevision(n => n + 1)}>Retry</button></p>}
        <form onSubmit={event => { event.preventDefault(); void operate('entry'); }}>
          <div className="admin-toolbar">
            <label>Parking zone<select value={zoneId} disabled={busy} onChange={e => { setZoneId(e.target.value); setResult(null); }} required>
              <option value="">Select a zone</option>{zones.map(zone => <option key={zone.id} value={zone.id}>{zone.name} ({zone.code})</option>)}
            </select></label>
            <label>Vehicle number<input autoComplete="off" placeholder="e.g. ABC-1234" maxLength={30} value={plate}
              disabled={busy} onChange={e => { setPlate(e.target.value.toUpperCase()); setResult(null); }} required /></label>
            <button className="btn btn-primary" type="submit" disabled={busy || !zoneId}>{busy ? 'Processing…' : 'Check In'}</button>
            <button className="btn btn-secondary" type="button" disabled={busy || !zoneId} onClick={() => void operate('exit')}>Check Out</button>
          </div>
        </form>
        <p className="muted">Check-in requires a reservation valid at this time in the selected zone. Spaces are assigned by the reservation.</p>
      </>}
    </div>
    {result && <div className="glass-panel admin-panel" role="status">
      <h2>{result.checkOutTime ? 'Vehicle checked out' : 'Vehicle checked in'}</h2>
      <p><strong>{result.vehiclePlate}</strong> · {result.zoneName} · Space {result.slotNumber}</p>
      <p>Entered: {new Date(result.checkInTime).toLocaleString()}</p>
      {result.checkOutTime && <>
        <p>Exited: {new Date(result.checkOutTime).toLocaleString()}</p>
        <h3>Final charge: {result.currency || settings.defaultCurrency} {result.totalFee.toFixed(2)}</h3>
        <p>Includes penalty: {result.currency} {result.penaltyFee.toFixed(2)}</p>
        <p>{paid ? 'Payment received at the gate.' : 'Collect payment at the gate, then record it below.'}</p>
        <button className="btn btn-primary" disabled={busy || paid} onClick={() => void recordPayment()}>{paid ? 'Payment recorded' : 'Record payment received'}</button>
      </>}
    </div>}
  </>;
}
