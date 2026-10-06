import { useEffect, useRef, useState, useContext } from 'react';
import { Link } from 'react-router-dom';
import { SettingsContext } from '../../App';
import { EmptyState, Loading, Notice, PageHeading } from '../../components/PageTools';
import { ArrowRight, CalendarDays, CheckCircle2, LogIn, LogOut, MapPin, Receipt } from 'lucide-react';
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
  const [action, setAction] = useState<'entry' | 'exit' | null>(null);
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
    pending.current = true; setBusy(true); setAction(action); setError(''); setResult(null); setPaid(false);
    try { setResult(await postData<GateSession>(`/api/gate/${action}`, { vehiclePlate, zoneId })); }
    catch (err) { setError(messageOf(err)); }
    finally { pending.current = false; setBusy(false); setAction(null); }
  }

  async function recordPayment() {
    if (!result || pending.current) return;
    pending.current = true; setBusy(true); setError('');
    try { await postData(`/api/admin/bookings/${result.bookingId}/pay`, {}); setPaid(true); }
    catch (err) { setError(messageOf(err)); }
    finally { pending.current = false; setBusy(false); }
  }

  const zone = zones.find(item => item.id === zoneId);
  const minutes = result?.checkOutTime ? Math.max(0, Math.round((new Date(result.checkOutTime).getTime() - new Date(result.checkInTime).getTime()) / 60000)) : 0;
  return <>
    <PageHeading title="Gate Entry / Exit" description="Enter the vehicle number to check a customer in or out of their reservation.">
      <Link className="btn btn-secondary" to="/bookings"><CalendarDays size={16} aria-hidden="true" />View reservations</Link>
    </PageHeading>
    <Notice error={error} />
    <div className="grid-split">
      <section className="card">
        <div className="card-header"><div><h2>Vehicle at the gate</h2><p>Check-in needs a reservation valid now in the selected zone. The bay comes from the reservation.</p></div></div>
        <div className="card-body">
          {loading ? <Loading label="Loading parking zones…" /> : zones.length === 0
            ? <EmptyState icon={MapPin} title="No zones available" action={<button className="btn btn-secondary" onClick={() => setRevision(n => n + 1)}>Retry</button>}>Create a parking zone before using the gate.</EmptyState>
            : <form className="stack" onSubmit={event => { event.preventDefault(); void operate('entry'); }}>
              <label className="field"><span className="field-label">Parking zone</span><select value={zoneId} disabled={busy} onChange={e => { setZoneId(e.target.value); setResult(null); }} required>
                <option value="">Select a zone</option>{zones.map(item => <option key={item.id} value={item.id}>{item.name} ({item.code})</option>)}
              </select></label>
              <label className="field"><span className="field-label">Vehicle number</span><input className="plate-input" autoComplete="off" spellCheck={false} placeholder="ABC-1234" maxLength={30} value={plate}
                disabled={busy} onChange={e => { setPlate(e.target.value.toUpperCase()); setResult(null); }} required /><span className="field-hint">Letters and numbers; spaces and dashes are ignored.</span></label>
              <div className="gate-actions">
                <button className="btn btn-primary btn-lg" type="submit" disabled={busy || !zoneId}><LogIn size={18} aria-hidden="true" />{action === 'entry' ? 'Checking in…' : 'Check in'}</button>
                <button className="btn btn-secondary btn-lg" type="button" disabled={busy || !zoneId} onClick={() => void operate('exit')}><LogOut size={18} aria-hidden="true" />{action === 'exit' ? 'Checking out…' : 'Check out'}</button>
              </div>
            </form>}
        </div>
      </section>
      <section className="card" aria-live="polite">
        {result ? <>
          <div className="card-header"><div className="result-head">
            <span className={`result-icon ${result.checkOutTime ? 'tone-info' : 'tone-good'}`}>{result.checkOutTime ? <Receipt size={20} aria-hidden="true" /> : <CheckCircle2 size={20} aria-hidden="true" />}</span>
            <div><h2>{result.checkOutTime ? 'Vehicle checked out' : 'Vehicle checked in'}</h2><p>{result.zoneName} · Bay {result.slotNumber}</p></div>
          </div></div>
          <div className="card-body">
            <dl className="kv">
              <dt>Vehicle</dt><dd>{result.vehiclePlate}</dd>
              <dt>Entered</dt><dd>{new Date(result.checkInTime).toLocaleString()}</dd>
              {result.checkOutTime && <><dt>Exited</dt><dd>{new Date(result.checkOutTime).toLocaleString()}</dd><dt>Duration</dt><dd>{Math.floor(minutes / 60)}h {minutes % 60}m</dd></>}
            </dl>
            {result.checkOutTime && <>
              <div className="receipt-total"><span>Total due</span><strong>{result.currency || settings.defaultCurrency} {result.totalFee.toFixed(2)}</strong></div>
              <p className="subtle">Includes overstay penalty of {result.currency || settings.defaultCurrency} {result.penaltyFee.toFixed(2)}</p>
            </>}
          </div>
          {result.checkOutTime && <div className="card-footer">
            {paid ? <span className="badge tone-good">Payment recorded</span> : <span className="subtle">Collect payment at the gate, then record it.</span>}
            <button className="btn btn-primary" style={{ marginLeft: 'auto' }} disabled={busy || paid} onClick={() => void recordPayment()}>{paid ? 'Payment recorded' : 'Record payment'}</button>
          </div>}
        </> : <EmptyState icon={ArrowRight} title="Waiting for a vehicle">{zone ? `Gate is set to ${zone.name}. ` : ''}Results and the final charge appear here after check-in or check-out.</EmptyState>}
      </section>
    </div>
  </>;
}
