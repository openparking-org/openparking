import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, CalendarCheck, CheckCircle2, Receipt } from 'lucide-react';
import { apiGet } from '../../lib/apiClient';
import { createBooking, quoteBooking, type FeeBreakdown } from './bookingApi';

interface Slot {
  id: string;
  slotNumber: string;
  status: number;
}

interface Zone {
  id: string;
  name: string;
  baseHourlyRate: number;
  slots: Slot[];
}

/** <input type="datetime-local"> wants local wall time with no zone suffix. */
function toLocalInputValue(date: Date): string {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}

function hoursFromNow(hours: number): string {
  const d = new Date();
  d.setMinutes(0, 0, 0);
  d.setHours(d.getHours() + hours);
  return toLocalInputValue(d);
}

export const NewBookingPage: React.FC = () => {
  const navigate = useNavigate();

  const [zones, setZones] = useState<Zone[]>([]);
  const [zoneId, setZoneId] = useState('');
  const [slotId, setSlotId] = useState('');
  const [startTime, setStartTime] = useState(hoursFromNow(1));
  const [endTime, setEndTime] = useState(hoursFromNow(3));

  const [quote, setQuote] = useState<FeeBreakdown | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [confirmed, setConfirmed] = useState<string | null>(null);

  useEffect(() => {
    apiGet<Zone[]>('/api/zones')
      .then((data: Zone[]) => {
        setZones(data);
        if (data.length > 0) setZoneId(data[0].id);
      })
      .catch((err: Error) => setError(err.message));
  }, []);

  const slots = useMemo(
    () => zones.find((z: Zone) => z.id === zoneId)?.slots ?? [],
    [zones, zoneId]
  );

  useEffect(() => {
    // Keep the slot selection valid whenever the zone changes.
    setSlotId(slots.length > 0 ? slots[0].id : '');
    setQuote(null);
  }, [slots]);

  const windowInvalid = new Date(endTime) <= new Date(startTime);
  const canSubmit = zoneId !== '' && slotId !== '' && !windowInvalid && !busy;

  const asIsoUtc = (local: string) => new Date(local).toISOString();

  const handleQuote = async () => {
    setError(null);
    setBusy(true);
    try {
      setQuote(await quoteBooking({ slotId, startTime: asIsoUtc(startTime), endTime: asIsoUtc(endTime) }));
    } catch (err) {
      setQuote(null);
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const handleConfirm = async () => {
    setError(null);
    setBusy(true);
    try {
      const result = await createBooking({ slotId, startTime: asIsoUtc(startTime), endTime: asIsoUtc(endTime) });
      setConfirmed(result.booking.qrCodeContent);
      setQuote(result.fee);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  if (confirmed) {
    return (
      <div className="glass-panel" style={{ padding: '28px', maxWidth: '520px' }}>
        <div className="form-alert form-alert-success" role="status">
          <CheckCircle2 size={18} />
          <span>Reservation confirmed.</span>
        </div>

        <h2 style={{ fontSize: '1.1rem', fontWeight: 700, marginBottom: '6px' }}>Digital parking pass</h2>
        <p style={{ fontSize: '0.84rem', color: 'var(--text-secondary)', marginBottom: '14px' }}>
          Present this code at the gate. Staff scan it to open your session.
        </p>

        <code
          style={{
            display: 'block', padding: '14px', borderRadius: 'var(--radius-md)',
            background: 'rgba(255,255,255,0.04)', border: '1px solid var(--border-color)',
            fontFamily: 'ui-monospace, monospace', fontSize: '0.92rem', letterSpacing: '0.04em',
            wordBreak: 'break-all', marginBottom: '18px',
          }}
        >
          {confirmed}
        </code>

        {quote && <FeeTable quote={quote} />}

        <div style={{ display: 'flex', gap: '10px', marginTop: '20px' }}>
          <button className="btn btn-primary" onClick={() => navigate('/bookings')}>
            View my bookings
          </button>
          <button
            className="btn btn-secondary"
            onClick={() => { setConfirmed(null); setQuote(null); }}
          >
            Book another
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="glass-panel" style={{ padding: '28px', maxWidth: '520px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '22px' }}>
        <div className="brand-icon" style={{ width: '42px', height: '42px', background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)' }}>
          <CalendarCheck size={22} />
        </div>
        <div>
          <h2 style={{ fontSize: '1.2rem', fontWeight: 700 }}>Reserve a slot</h2>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.86rem' }}>
            Prices come from the server, never the browser.
          </p>
        </div>
      </div>

      {error && (
        <div className="form-alert form-alert-error" role="alert">
          <AlertCircle size={16} />
          <span>{error}</span>
        </div>
      )}

      <div className="field">
        <label htmlFor="zone">Zone</label>
        <select id="zone" value={zoneId} onChange={(e: React.ChangeEvent<HTMLSelectElement>) => setZoneId(e.target.value)}>
          {zones.length === 0 && <option value="">No zones available</option>}
          {zones.map((z: Zone) => (
            <option key={z.id} value={z.id}>{z.name} — {z.baseHourlyRate.toFixed(2)}/h</option>
          ))}
        </select>
      </div>

      <div className="field">
        <label htmlFor="slot">Slot</label>
        <select id="slot" value={slotId} onChange={(e: React.ChangeEvent<HTMLSelectElement>) => setSlotId(e.target.value)}>
          {slots.length === 0 && <option value="">No slots in this zone</option>}
          {slots.map((s: Slot) => (
            <option key={s.id} value={s.id}>{s.slotNumber}</option>
          ))}
        </select>
      </div>

      <div className="field">
        <label htmlFor="start">From</label>
        <input id="start" type="datetime-local" value={startTime}
          onChange={(e: React.ChangeEvent<HTMLInputElement>) => { setStartTime(e.target.value); setQuote(null); }} />
      </div>

      <div className="field">
        <label htmlFor="end">Until</label>
        <input id="end" type="datetime-local" value={endTime} aria-invalid={windowInvalid}
          aria-describedby={windowInvalid ? 'end-error' : undefined}
          onChange={(e: React.ChangeEvent<HTMLInputElement>) => { setEndTime(e.target.value); setQuote(null); }} />
        {windowInvalid && <span className="field-error" id="end-error">The end time must be after the start time.</span>}
      </div>

      {quote && (
        <div style={{ marginBottom: '18px' }}>
          <FeeTable quote={quote} />
        </div>
      )}

      <div style={{ display: 'flex', gap: '10px' }}>
        <button className="btn btn-secondary" onClick={handleQuote} disabled={!canSubmit}>
          <Receipt size={16} />
          {busy ? 'Working…' : 'Get price'}
        </button>
        <button className="btn btn-primary" onClick={handleConfirm} disabled={!canSubmit}>
          <CheckCircle2 size={16} />
          Confirm booking
        </button>
      </div>
    </div>
  );
};

const FeeTable: React.FC<{ quote: FeeBreakdown }> = ({ quote }) => {
  const money = (v: number) => v.toFixed(2);

  return (
    <div style={{ borderRadius: 'var(--radius-md)', border: '1px solid var(--border-color)', background: 'rgba(255,255,255,0.02)', padding: '14px' }}>
      <Row label="Billable hours" value={`${quote.billableHours} (billed per started hour)`} />
      <Row label="Base rate" value={`${money(quote.baseHourlyRate)} / hour`} />
      {quote.appliedSurgeMultiplier !== 1 && (
        <Row
          label="Surge"
          value={`×${quote.appliedSurgeMultiplier}${quote.surgeWasCapped ? ' (capped by policy)' : ''}`}
        />
      )}
      <Row label="Effective rate" value={`${money(quote.effectiveHourlyRate)} / hour`} />
      <div style={{ borderTop: '1px solid var(--border-color)', marginTop: '10px', paddingTop: '10px' }}>
        <Row label="Total" value={money(quote.amount)} strong />
      </div>
    </div>
  );
};

const Row: React.FC<{ label: string; value: string; strong?: boolean }> = ({ label, value, strong }) => (
  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: strong ? '0.96rem' : '0.86rem', padding: '3px 0' }}>
    <span style={{ color: 'var(--text-secondary)' }}>{label}</span>
    <span style={{ fontWeight: strong ? 700 : 500 }}>{value}</span>
  </div>
);
