import { useEffect, useState } from 'react';
import { useAdminPage } from '../../hooks/useAdminPage';
import { allZones, postData, messageOf, type Zone } from '../../services/adminService';
import { Notice, PageHeading, Pagination } from '../../components/PageTools';

interface HardwareEvent { id: string; action: string; payloadJson: string; createdAt: string }
function eventPayload(value: string): { deviceId?: string; zoneId?: string; message?: string; severity?: string } { try { return JSON.parse(value); } catch { return { message: value }; } }
export function HardwarePage() {
  const list = useAdminPage<HardwareEvent>('/api/admin/hardware');
  const [zones, setZones] = useState<Zone[]>([]);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ zoneId: '', deviceId: '', type: 'Heartbeat', severity: 'Info', message: '' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => { const controller = new AbortController(); allZones(controller.signal).then(setZones).catch(err => { if (!controller.signal.aborted) setError(messageOf(err)); }); return () => controller.abort(); }, []);
  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setBusy(true); setError('');
    try { await postData('/api/admin/hardware', form); setOpen(false); list.reload(); }
    catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  return <><PageHeading title="Hardware & Sensor Logs" description="Recorded device events from your parking zones."><button className="btn btn-primary" onClick={() => setOpen(!open)}>Record event</button></PageHeading><Notice error={error || list.error} />
    {open && <form className="glass-panel admin-form" onSubmit={submit}><h2>Record a device event</h2><div className="admin-form-grid"><label>Zone<select required value={form.zoneId} onChange={e => setForm({ ...form, zoneId: e.target.value })}><option value="">Select zone</option>{zones.map(z => <option key={z.id} value={z.id}>{z.name}</option>)}</select></label><label>Device ID<input required maxLength={128} value={form.deviceId} onChange={e => setForm({ ...form, deviceId: e.target.value })} /></label><label>Event type<select value={form.type} onChange={e => setForm({ ...form, type: e.target.value })}>{['Heartbeat', 'Entry', 'Exit', 'Offline', 'SensorUpdate'].map(type => <option key={type}>{type}</option>)}</select></label><label>Severity<select value={form.severity} onChange={e => setForm({ ...form, severity: e.target.value })}>{['Info', 'Warning', 'Error'].map(s => <option key={s}>{s}</option>)}</select></label><label className="span-two">Message<input required maxLength={1000} value={form.message} onChange={e => setForm({ ...form, message: e.target.value })} /></label></div><button className="btn btn-primary" disabled={busy}>Save event</button></form>}
    <div className="glass-panel admin-panel"><div className="admin-toolbar"><input aria-label="Search device events" placeholder="Search device or message" value={list.search} onChange={e => list.setSearch(e.target.value)} /><button className="btn btn-secondary" onClick={list.reload}>Refresh</button></div>{list.loading ? <p role="status">Loading events…</p> : !list.error && <div className="admin-table-wrap"><table className="admin-table"><thead><tr><th>Time</th><th>Device / zone</th><th>Event</th><th>Severity</th><th>Message</th></tr></thead><tbody>{list.data?.items.map(event => { const payload = eventPayload(event.payloadJson); return <tr key={event.id}><td>{new Date(event.createdAt).toLocaleString()}</td><td>{payload.deviceId}<small>{zones.find(z => z.id === payload.zoneId)?.name || payload.zoneId}</small></td><td>{event.action}</td><td>{payload.severity}</td><td>{payload.message}</td></tr>; })}</tbody></table>{list.data?.items.length === 0 && <p>No device events recorded yet. Record an event or connect your devices to the hardware event API.</p>}</div>}<Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} /></div>
  </>;
}
