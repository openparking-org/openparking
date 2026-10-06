import { useEffect, useState } from 'react';
import { useAdminPage } from '../../hooks/useAdminPage';
import { allZones, postData, messageOf, type Zone } from '../../services/adminService';
import { EmptyState, Loading, Notice, PageHeading, Pagination, StatusBadge } from '../../components/PageTools';
import { humanize } from '../../lib/status';
import { Plus, Radio, RefreshCw, Search, X } from 'lucide-react';

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
  return <>
    <PageHeading title="Hardware & Sensor Logs" description="Device events recorded across your parking zones.">{!open && <button className="btn btn-primary" onClick={() => setOpen(true)}><Plus size={16} aria-hidden="true" />Record event</button>}</PageHeading>
    <Notice error={error || list.error} />
    {open && <form className="card" onSubmit={submit} style={{ marginBottom: 20 }}>
      <div className="card-header"><div><h2>Record a device event</h2><p>Use this to log a manual observation or test the hardware pipeline.</p></div><button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Close form" onClick={() => setOpen(false)}><X size={18} /></button></div>
      <div className="card-body form-grid">
        <label className="field"><span className="field-label">Zone</span><select required value={form.zoneId} onChange={e => setForm({ ...form, zoneId: e.target.value })}><option value="">Select zone</option>{zones.map(z => <option key={z.id} value={z.id}>{z.name}</option>)}</select></label>
        <label className="field"><span className="field-label">Device ID</span><input required maxLength={128} placeholder="e.g. gate-cam-01" value={form.deviceId} onChange={e => setForm({ ...form, deviceId: e.target.value })} /></label>
        <label className="field"><span className="field-label">Event type</span><select value={form.type} onChange={e => setForm({ ...form, type: e.target.value })}>{['Heartbeat', 'Entry', 'Exit', 'Offline', 'SensorUpdate'].map(type => <option key={type} value={type}>{humanize(type)}</option>)}</select></label>
        <div className="field"><span className="field-label" id="severity-label">Severity</span><div className="segmented" role="group" aria-labelledby="severity-label">{['Info', 'Warning', 'Error'].map(level => <button key={level} type="button" aria-pressed={form.severity === level} onClick={() => setForm({ ...form, severity: level })}>{level}</button>)}</div></div>
        <label className="field span-two"><span className="field-label">Message</span><input required maxLength={1000} placeholder="What happened?" value={form.message} onChange={e => setForm({ ...form, message: e.target.value })} /></label>
      </div>
      <div className="card-footer"><button className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save event'}</button><button type="button" className="btn btn-secondary" onClick={() => setOpen(false)}>Cancel</button></div>
    </form>}
    <div className="card">
      <div className="toolbar"><div className="input-affix"><Search size={16} aria-hidden="true" /><input type="search" aria-label="Search device events" placeholder="Search device or message" value={list.search} onChange={e => list.setSearch(e.target.value)} /></div><span className="spacer" /><button className="btn btn-secondary btn-sm" onClick={list.reload}><RefreshCw size={14} aria-hidden="true" />Refresh</button></div>
      {list.loading ? <Loading label="Loading events…" /> : !list.error && (list.data?.items.length === 0
        ? <EmptyState icon={Radio} title="No device events yet">Record an event, or connect devices to the hardware event API.</EmptyState>
        : <div className="table-wrap"><table className="data-table"><thead><tr><th>Time</th><th>Device</th><th>Event</th><th>Severity</th><th>Message</th></tr></thead><tbody>{list.data?.items.map(event => { const payload = eventPayload(event.payloadJson); return <tr key={event.id}>
          <td className="tabular" style={{ whiteSpace: 'nowrap' }}>{new Date(event.createdAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })}</td>
          <td><span className="cell-title mono">{payload.deviceId || '—'}</span><span className="cell-sub">{zones.find(z => z.id === payload.zoneId)?.name || payload.zoneId}</span></td>
          <td>{humanize(event.action)}</td>
          <td>{payload.severity ? <StatusBadge status={payload.severity} /> : '—'}</td>
          <td style={{ maxWidth: 420 }}>{payload.message}</td>
        </tr>; })}</tbody></table></div>)}
      <Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} />
    </div>
  </>;
}
