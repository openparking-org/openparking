import { lazy, Suspense, useState } from 'react';
import { Link } from 'react-router-dom';
import { apiClient } from '../../lib/apiClient';
import { useAdminPage } from '../../hooks/useAdminPage';
import { messageOf, type Zone } from '../../services/adminService';
import { EmptyState, Loading, Notice, PageHeading, Pagination } from '../../components/PageTools';
import { useConfirm } from '../../components/ConfirmDialog';
import { Layers, MapPin, Pencil, Plus, RefreshCw, Search, Trash2, X } from 'lucide-react';

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
  const confirm = useConfirm();
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
    if (!await confirm({ title: `Delete ${zone.name}?`, body: 'This removes the zone and its mapped bays. Zones with booking history cannot be deleted.', confirmLabel: 'Delete zone', danger: true })) return;
    setBusy(true); setError(''); setMessage('');
    try { await apiClient.delete(`/api/zones/${zone.id}`); setMessage('Zone deleted.'); list.reload(); }
    catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  const edit = (zone: Zone) => { setForm({ ...zone, latitude: String(zone.latitude), longitude: String(zone.longitude) }); setEditing(zone.id); setOpen(true); window.scrollTo({ top: 0, behavior: 'smooth' }); };
  return <>
    <PageHeading title="Parking Zones" description="Manage locations, hourly rates and bay capacity.">{!open && <button className="btn btn-primary" onClick={() => { setForm(blank); setEditing(null); setOpen(true); }}><Plus size={16} aria-hidden="true" />Add zone</button>}</PageHeading>
    <Notice error={error || list.error} message={message} />
    {open && <form className="card" onSubmit={save} style={{ marginBottom: 20 }}>
      <div className="card-header"><div><h2>{editing ? 'Edit zone' : 'New zone'}</h2><p>{editing ? 'Code and capacity are fixed once a zone exists.' : 'Pick the vehicle entrance, then set the rate and capacity.'}</p></div><button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Close form" onClick={() => setOpen(false)}><X size={18} /></button></div>
      <div className="card-body grid-2">
        <Suspense fallback={<Loading label="Loading location picker…" />}><EntranceLocationPicker key={editing || 'new'} position={position} disabled={busy} onChange={point => setForm(current => ({ ...current, latitude: point.lat.toFixed(6), longitude: point.lng.toFixed(6) }))} /></Suspense>
        <div className="form-grid" style={{ alignContent: 'start' }}>
          <label className="field span-two"><span className="field-label">Zone name</span><input required minLength={2} maxLength={128} placeholder="e.g. Main Campus Lot" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></label>
          <label className="field"><span className="field-label">Code</span><input required maxLength={16} disabled={!!editing} placeholder="Z-MAIN" value={form.code} onChange={e => setForm({ ...form, code: e.target.value.toUpperCase() })} /></label>
          <label className="field"><span className="field-label">Capacity</span><div className="input-unit"><input required type="number" min={1} max={10000} disabled={!!editing} value={form.totalCapacity} onChange={e => setForm({ ...form, totalCapacity: Number(e.target.value) })} /><span>bays</span></div></label>
          <label className="field span-two"><span className="field-label">Hourly rate</span><div className="input-unit"><input required type="number" min={0} max={1000} step="0.01" value={form.baseHourlyRate} onChange={e => setForm({ ...form, baseHourlyRate: Number(e.target.value) })} /><span>per hour</span></div></label>
          <label className="field"><span className="field-label">Entrance latitude</span><input required type="number" step="any" min={-90} max={90} disabled={busy} value={form.latitude} onChange={e => setForm({ ...form, latitude: e.target.value })} /></label>
          <label className="field"><span className="field-label">Entrance longitude</span><input required type="number" step="any" min={-180} max={180} disabled={busy} value={form.longitude} onChange={e => setForm({ ...form, longitude: e.target.value })} /></label>
        </div>
      </div>
      <div className="card-footer"><button className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : editing ? 'Save changes' : 'Create zone'}</button><button type="button" className="btn btn-secondary" onClick={() => setOpen(false)}>Cancel</button></div>
    </form>}
    <div className="card">
      <div className="toolbar"><div className="input-affix"><Search size={16} aria-hidden="true" /><input type="search" aria-label="Search zones" placeholder="Search by name or code" value={list.search} onChange={e => list.setSearch(e.target.value)} /></div><span className="spacer" /><button className="btn btn-secondary btn-sm" onClick={list.reload}><RefreshCw size={14} aria-hidden="true" />Refresh</button></div>
      {list.loading ? <Loading label="Loading zones…" /> : !list.error && (list.data?.items.length === 0
        ? <EmptyState icon={MapPin} title={list.search ? 'No zones match your search' : 'No parking zones yet'}>{list.search ? 'Try a different name or code.' : 'Create a zone to start mapping bays and taking reservations.'}</EmptyState>
        : <div className="table-wrap"><table className="data-table"><thead><tr><th>Zone</th><th>Bays</th><th>Live status</th><th className="num">Rate</th><th className="actions"><span className="sr-only">Actions</span></th></tr></thead><tbody>{list.data?.items.map(zone => {
          const total = zone.availableCount + zone.reservedCount + zone.occupiedCount;
          const width = (n: number) => total ? `${(n / total) * 100}%` : '0%';
          return <tr key={zone.id}>
            <td><span className="cell-title">{zone.name}</span><span className="cell-sub">{zone.code} · {zone.latitude.toFixed(5)}, {zone.longitude.toFixed(5)}</span></td>
            <td><span className="tabular">{zone.mappedCount} / {zone.totalCapacity}</span><span className="cell-sub">mapped / capacity</span></td>
            <td style={{ minWidth: 200 }}><div className="meter" role="img" aria-label={`${zone.availableCount} available, ${zone.reservedCount} reserved, ${zone.occupiedCount} occupied`}>{zone.availableCount > 0 && <span className="good" style={{ width: width(zone.availableCount) }} />}{zone.reservedCount > 0 && <span className="warn" style={{ width: width(zone.reservedCount) }} />}{zone.occupiedCount > 0 && <span className="crit" style={{ width: width(zone.occupiedCount) }} />}</div><span className="cell-sub">{zone.availableCount} available · {zone.reservedCount} reserved · {zone.occupiedCount} occupied</span></td>
            <td className="num"><span className="cell-title">{zone.currency} {zone.baseHourlyRate.toFixed(2)}</span><span className="cell-sub">per hour</span></td>
            <td className="actions"><div>
              <Link className="btn btn-secondary btn-sm" to={`/slot-mapping?zone=${zone.id}`}><Layers size={14} aria-hidden="true" />Map bays</Link>
              <button className="btn btn-secondary btn-icon btn-sm" aria-label={`Edit ${zone.name}`} title="Edit" onClick={() => edit(zone)}><Pencil size={14} /></button>
              <button className="btn btn-danger btn-icon btn-sm" aria-label={`Delete ${zone.name}`} title="Delete" disabled={busy} onClick={() => void remove(zone)}><Trash2 size={14} /></button>
            </div></td>
          </tr>;
        })}</tbody></table></div>)}
      <Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} />
    </div>
  </>;
}
