import { lazy, Suspense, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiClient } from '../../lib/apiClient';
import { useAdminPage } from '../../hooks/useAdminPage';
import { messageOf, type Zone } from '../../services/adminService';
import { Notice, PageHeading, Pagination } from '../../components/PageTools';

const EntranceLocationPicker = lazy(() => import('./EntranceLocationPicker'));
const blank = { name: '', code: '', latitude: '', longitude: '', baseHourlyRate: 5, totalCapacity: 1 };
export function ZonesPage() {
  const list = useAdminPage<Zone>('/api/admin/zones');
  const [form, setForm] = useState(blank);
  const [editing, setEditing] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const latitude = Number(form.latitude), longitude = Number(form.longitude);
  const position = form.latitude.trim() && form.longitude.trim() && Number.isFinite(latitude) && Number.isFinite(longitude)
    && Math.abs(latitude) <= 90 && Math.abs(longitude) <= 180 ? { lat: latitude, lng: longitude } : null;
  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!position) { setError('Select the vehicle entrance or enter valid latitude and longitude.'); return; }
    setBusy(true); setError(''); setMessage('');
    try {
      if (editing) await apiClient.put(`/api/zones/${editing}`, { name: form.name, baseHourlyRate: form.baseHourlyRate, latitude, longitude });
      else await apiClient.post('/api/zones', { ...form, latitude, longitude });
      setOpen(false); setMessage(editing ? 'Zone updated.' : 'Zone created. Add bays in Slot Mapping.'); list.reload();
    } catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  const remove = async (zone: Zone) => {
    if (!window.confirm(`Delete ${zone.name}? Zones with booking history cannot be deleted.`)) return;
    setBusy(true); setError(''); setMessage('');
    try { await apiClient.delete(`/api/zones/${zone.id}`); setMessage('Zone deleted.'); list.reload(); }
    catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  return <>
    <PageHeading title="Parking Zones" description="Manage locations, rates, and parking inventory."><button className="btn btn-primary" onClick={() => { setForm(blank); setEditing(null); setOpen(true); }}>Add zone</button></PageHeading>
    <Notice error={error || list.error} message={message} />
    {open && <form className="glass-panel admin-form" onSubmit={save}><h2>{editing ? 'Edit zone' : 'New zone'}</h2>
    <Suspense fallback={<p role="status">Loading location picker…</p>}><EntranceLocationPicker key={editing || 'new'} position={position} disabled={busy} onChange={point => setForm(current => ({ ...current, latitude: point.lat.toFixed(6), longitude: point.lng.toFixed(6) }))} /></Suspense>
    <div className="admin-form-grid">
      <label>Name<input required minLength={2} maxLength={128} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></label>
      <label>Code<input required maxLength={16} disabled={!!editing} value={form.code} onChange={e => setForm({ ...form, code: e.target.value.toUpperCase() })} /></label>
      <label>Entrance latitude<input required type="number" step="any" min={-90} max={90} disabled={busy} value={form.latitude} onChange={e => setForm({ ...form, latitude: e.target.value })} /></label>
      <label>Entrance longitude<input required type="number" step="any" min={-180} max={180} disabled={busy} value={form.longitude} onChange={e => setForm({ ...form, longitude: e.target.value })} /></label>
      <label>Hourly rate<input required type="number" min={0} max={1000} step="0.01" value={form.baseHourlyRate} onChange={e => setForm({ ...form, baseHourlyRate: Number(e.target.value) })} /></label>
      <label>Capacity<input required type="number" min={1} max={10000} disabled={!!editing} value={form.totalCapacity} onChange={e => setForm({ ...form, totalCapacity: Number(e.target.value) })} /></label>
    </div><div className="admin-actions"><button className="btn btn-primary" disabled={busy}>Save zone</button><button type="button" className="btn btn-secondary" onClick={() => setOpen(false)}>Cancel</button></div></form>}
    <div className="glass-panel admin-panel"><div className="admin-toolbar"><input aria-label="Search zones" placeholder="Search name or code" value={list.search} onChange={e => list.setSearch(e.target.value)} /><button className="btn btn-secondary" onClick={list.reload}>Refresh</button></div>
      {list.loading ? <p role="status">Loading zones…</p> : !list.error && <div className="admin-table-wrap"><table className="admin-table"><thead><tr><th>Zone</th><th>Bays</th><th>Available / occupied / reserved</th><th>Rate</th><th>Actions</th></tr></thead><tbody>{list.data?.items.map(zone => <tr key={zone.id}><td><strong>{zone.name}</strong><small>{zone.code}</small><small>Entrance: {zone.latitude.toFixed(6)}, {zone.longitude.toFixed(6)}</small></td><td>{zone.mappedCount} mapped / {zone.totalCapacity} capacity</td><td>{zone.availableCount} / {zone.occupiedCount} / {zone.reservedCount}</td><td>{zone.currency} {zone.baseHourlyRate.toFixed(2)}/hr</td><td><div className="admin-actions"><Link className="btn btn-secondary" to={`/slot-mapping?zone=${zone.id}`}>Map</Link><button className="btn btn-secondary" onClick={() => { setForm({ ...zone, latitude: String(zone.latitude), longitude: String(zone.longitude) }); setEditing(zone.id); setOpen(true); }}>Edit</button><button className="btn btn-secondary" disabled={busy} onClick={() => void remove(zone)}>Delete</button></div></td></tr>)}</tbody></table>{list.data?.items.length === 0 && <p>No zones found. Create a zone to get started.</p>}</div>}
      <Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} />
    </div>
  </>;
}
