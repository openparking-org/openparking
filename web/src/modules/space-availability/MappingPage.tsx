import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { apiClient } from '../../lib/apiClient';
import { allZones, getData, postData, messageOf, type FloorPlan, type Slot, type Zone } from '../../services/adminService';
import { Notice, PageHeading } from '../../components/PageTools';

interface EditorSlot extends Slot { localId: string; isNew?: boolean }
interface Waypoint { id: string; x: number; y: number; neighbors: string[] }
const types = ['Standard', 'Compact', 'Large', 'EV', 'Accessible'];
const color: Record<string, string> = { Available: '#166534', Reserved: '#92400e', Occupied: '#991b1b', Maintenance: '#475569' };
function validImage(value: string) { return !value || /^https?:\/\//i.test(value) || /^data:image\/(png|jpeg|webp);base64,/i.test(value); }
export function MappingPage() {
  const [params] = useSearchParams();
  const [zones, setZones] = useState<Zone[]>([]);
  const [zoneId, setZoneId] = useState(params.get('zone') || '');
  const [floor, setFloor] = useState(0);
  const [plans, setPlans] = useState<FloorPlan[]>([]);
  const [slots, setSlots] = useState<EditorSlot[]>([]);
  const [waypoints, setWaypoints] = useState<Waypoint[]>([]);
  const [image, setImage] = useState('');
  const [floorName, setFloorName] = useState('Ground floor');
  const [tool, setTool] = useState<'select' | 'slot' | 'waypoint'>('select');
  const [selected, setSelected] = useState('');
  const [dirty, setDirty] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [revision, setRevision] = useState(0);
  const [detections, setDetections] = useState<Array<{ slot_code: string; confidence: number; type: string; bounds: number[][] }>>([]);
  const zone = zones.find(z => z.id === zoneId);
  useEffect(() => {
    const controller = new AbortController();
    allZones(controller.signal).then(items => { setZones(items); setZoneId(current => current || items[0]?.id || ''); }).catch(err => { if (!controller.signal.aborted) setError(messageOf(err)); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, []);
  useEffect(() => {
    if (!zoneId) return;
    const controller = new AbortController();
    setLoading(true); setError(''); setSelected(''); setDetections([]);
    Promise.all([getData<{ slots: Slot[] }>(`/api/zones/${zoneId}`, controller.signal), getData<FloorPlan[]>(`/api/admin/zones/${zoneId}/floor-plans`, controller.signal)]).then(([detail, floors]) => {
      if (controller.signal.aborted) return;
      setPlans(floors);
      const plan = floors.find(f => f.floorOrder === floor);
      setImage(plan?.imageUrl || ''); setFloorName(plan?.floorName || `Floor ${floor}`);
      let graph: Waypoint[] = [];
      try { graph = JSON.parse(plan?.waypointGraphJson || '[]'); if (!Array.isArray(graph)) graph = []; } catch { graph = []; }
      setWaypoints(graph);
      setSlots(detail.slots.filter(s => s.floor === floor).map((s, index) => ({ ...s, localId: s.id, canvasX: s.canvasX ?? 40 + (index % 10) * 92, canvasY: s.canvasY ?? 50 + Math.floor(index / 10) * 80, canvasWidth: s.canvasWidth ?? 80, canvasHeight: s.canvasHeight ?? 55 })));
      setDirty(false);
    }).catch(err => { if (!controller.signal.aborted) setError(messageOf(err)); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [zoneId, floor, revision]);
  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ''; };
    const navigate = (event: MouseEvent) => {
      const link = (event.target as Element).closest?.('a');
      if (link && link.pathname !== window.location.pathname && !window.confirm('Discard unsaved layout changes?')) { event.preventDefault(); event.stopPropagation(); }
    };
    window.addEventListener('beforeunload', warn); document.addEventListener('click', navigate, true);
    return () => { window.removeEventListener('beforeunload', warn); document.removeEventListener('click', navigate, true); };
  }, [dirty]);
  const discard = () => !dirty || window.confirm('Discard unsaved layout changes?');
  const changeSlot = (id: string, changes: Partial<EditorSlot>) => { setSlots(current => current.map(slot => slot.localId === id ? { ...slot, ...changes } : slot)); setDirty(true); };
  const addSlot = (x = 40, y = 40) => {
    const id = `new-${crypto.randomUUID()}`;
    let n = slots.length + 1;
    while (slots.some(s => s.slotNumber === `F${floor}-${n}`)) n++;
    setSlots(current => [...current, { id: '', localId: id, isNew: true, slotNumber: `F${floor}-${n}`, floor, type: 'Standard', status: 'Available', canvasX: x, canvasY: y, canvasWidth: 80, canvasHeight: 55 }]); setSelected(id); setDirty(true);
  };
  const addWaypoint = (x = 50, y = 50) => { setWaypoints(current => [...current, { id: `wp-${crypto.randomUUID().slice(0, 8)}`, x, y, neighbors: [] }]); setDirty(true); };
  const save = async () => {
    setBusy(true); setError(''); setMessage('');
    try {
      if (!validImage(image)) throw new Error('Use an http(s) image URL or upload an image.');
      if (!floorName.trim()) throw new Error('Enter a floor name.');
      const waypointIds = new Set(waypoints.map(w => w.id));
      if (waypoints.some(w => w.x < 0 || w.x > 1000 || w.y < 0 || w.y > 600 || w.neighbors.some(id => !waypointIds.has(id.trim()))))
        throw new Error('Waypoint positions must be inside the canvas and neighbors must refer to existing waypoint IDs.');
      if (slots.some(s => s.nearestWaypointId && !waypointIds.has(s.nearestWaypointId))) throw new Error('A bay refers to a removed waypoint. Select a new nearest waypoint.');
      const plan = await postData<{ id: string }>(`/api/zones/${zoneId}/floor-plans`, { floorName, floorOrder: floor, imageUrl: image, imageWidthPx: 1000, imageHeightPx: 600, waypointGraphJson: JSON.stringify(waypoints) });
      try {
        await apiClient.put(`/api/admin/zones/${zoneId}/floors/${floor}/slots`, { floorPlanId: plan.id, slots: slots.map(slot => ({ ...slot, id: slot.isNew ? null : slot.id, assignedSensorId: slot.assignedSensorId || null, assignedCameraId: slot.assignedCameraId || null })) });
      } catch (err) { throw new Error(`Floor plan saved, but bays were not saved: ${messageOf(err)}. Your bay edits are still available; correct them and retry.`); }
      setDirty(false); setMessage('Floor plan and bays saved.'); setRevision(n => n + 1);
    } catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  const upload = (file?: File) => {
    if (!file) return;
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size > 3 * 1024 * 1024) { setError('Choose a PNG, JPEG, or WebP image under 3 MB.'); return; }
    const reader = new FileReader(); reader.onload = () => { setImage(String(reader.result)); setDirty(true); }; reader.readAsDataURL(file);
  };
  const detect = async () => {
    if (!zone) return; setBusy(true); setError(''); setMessage('');
    try {
      const result = await postData<{ detected_slots: typeof detections; algorithm: string }>('/api/admin/ai/slot-detection', { north: zone.latitude + .0006, south: zone.latitude - .0006, east: zone.longitude + .0009, west: zone.longitude - .0009, cols: 5, rows: 4, slot_prefix: `AI-F${floor}` });
      setDetections(result.detected_slots); setMessage(`${result.detected_slots.length} proposed bays. Method: ${result.algorithm}. Review before adding.`);
    } catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  const importDetections = () => {
    setSlots(current => [...current, ...detections.filter(d => !current.some(s => s.slotNumber === d.slot_code)).map((d, index) => ({ id: '', localId: `new-${crypto.randomUUID()}`, isNew: true, slotNumber: d.slot_code, type: types.includes(d.type) ? d.type : 'Standard', status: 'Available', floor, boundingBoxJson: JSON.stringify(d.bounds), canvasX: 40 + (index % 10) * 92, canvasY: 50 + Math.floor(index / 10) * 80, canvasWidth: 80, canvasHeight: 55 }))]);
    setDirty(true); setDetections([]); setMessage('Proposals added to your draft. Adjust their positions and save.');
  };
  const selectedSlot = slots.find(s => s.localId === selected);
  return <><PageHeading title="Slot Mapping" description="Design bays and indoor paths, assign devices, and save each floor."><button className="btn btn-primary" disabled={loading || busy || !zoneId || !dirty} onClick={() => void save()}>{busy ? 'Saving…' : 'Save layout'}</button></PageHeading><Notice error={error} message={message} />
    <div className="glass-panel admin-panel"><div className="admin-toolbar"><label>Zone<select value={zoneId} disabled={busy} onChange={e => { if (discard()) { setZoneId(e.target.value); setFloor(0); } }}>{zones.map(z => <option key={z.id} value={z.id}>{z.name}</option>)}</select></label><label>Floor<input type="number" value={floor} disabled={busy} onChange={e => { if (discard()) setFloor(Number(e.target.value)); }} /></label><button className="btn btn-secondary" disabled={busy} onClick={() => { if (discard()) setRevision(n => n + 1); }}>Reload</button><span>{dirty ? 'Unsaved changes' : 'Saved layout'}</span></div><p className="muted">Existing floors: {plans.map(f => `${f.floorName} (${f.floorOrder})`).join(', ') || 'None yet'}. Add a floor by changing its number.</p>
    {loading ? <p role="status">Loading layout…</p> : !zoneId ? <p>Create a zone before mapping bays.</p> : <><div className="admin-form-grid"><label>Floor name<input value={floorName} onChange={e => { setFloorName(e.target.value); setDirty(true); }} /></label><label>Blueprint URL<input value={image.startsWith('data:') ? '' : image} placeholder={image.startsWith('data:') ? 'Uploaded image selected' : 'https://…'} onChange={e => { setImage(e.target.value); setDirty(true); }} /></label><label>Upload blueprint<input type="file" accept="image/png,image/jpeg,image/webp" onChange={e => upload(e.target.files?.[0])} /></label></div><div className="admin-toolbar"><button className="btn btn-secondary" aria-pressed={tool === 'select'} onClick={() => setTool('select')}>Select</button><button className="btn btn-secondary" aria-pressed={tool === 'slot'} onClick={() => setTool('slot')}>Place bay</button><button className="btn btn-secondary" aria-pressed={tool === 'waypoint'} onClick={() => setTool('waypoint')}>Place waypoint</button><button className="btn btn-secondary" onClick={() => addSlot()}>Add bay</button><button className="btn btn-secondary" onClick={() => addWaypoint()}>Add waypoint</button><button className="btn btn-secondary" disabled={busy} onClick={() => void detect()}>Propose bays with AI</button></div>
    <svg className="mapping-canvas" viewBox="0 0 1000 600" role="img" aria-label="Floor plan editor; use bay properties and waypoint controls below for keyboard editing" onClick={event => { const rect = event.currentTarget.getBoundingClientRect(); const x = Math.max(0, Math.min(920, (event.clientX - rect.left) / rect.width * 1000)); const y = Math.max(0, Math.min(545, (event.clientY - rect.top) / rect.height * 600)); if (tool === 'slot') addSlot(x, y); if (tool === 'waypoint') addWaypoint(x, y); }}>
      <defs><pattern id="grid" width="20" height="20" patternUnits="userSpaceOnUse"><path d="M 20 0 L 0 0 0 20" fill="none" stroke="#cbd5e1" strokeWidth=".5" /></pattern></defs><rect width="1000" height="600" fill="url(#grid)" />{image && validImage(image) && <image href={image} width="1000" height="600" opacity=".65" preserveAspectRatio="none" />}
      {waypoints.flatMap(w => w.neighbors.map(id => { const target = waypoints.find(p => p.id === id); return target ? <line key={`${w.id}-${id}`} x1={w.x} y1={w.y} x2={target.x} y2={target.y} stroke="#2563eb" strokeWidth="3" /> : null; }))}
      {slots.map(slot => <g key={slot.localId} onClick={e => { e.stopPropagation(); setSelected(slot.localId); }}><rect x={slot.canvasX} y={slot.canvasY} width={slot.canvasWidth} height={slot.canvasHeight} rx="5" fill={color[slot.status] || '#475569'} stroke={selected === slot.localId ? '#2563eb' : '#fff'} strokeWidth={selected === slot.localId ? 4 : 1} /><text x={(slot.canvasX || 0) + 8} y={(slot.canvasY || 0) + 23} fill="white" fontSize="13">{slot.slotNumber}</text><text x={(slot.canvasX || 0) + 8} y={(slot.canvasY || 0) + 43} fill="white" fontSize="10">{slot.type}</text></g>)}{waypoints.map(w => <g key={w.id}><circle cx={w.x} cy={w.y} r="7" fill="#2563eb" /><text x={w.x + 10} y={w.y} fontSize="10">{w.id}</text></g>)}
    </svg>
    {detections.length > 0 && <div className="admin-panel"><h3>Review proposed bays</h3><p>{detections.map(d => `${d.slot_code}: ${(d.confidence * 100).toFixed(0)}%`).join(' · ')}</p><button className="btn btn-primary" onClick={importDetections}>Add proposals to draft</button><button className="btn btn-secondary" onClick={() => setDetections([])}>Discard proposals</button></div>}
    <div className="admin-toolbar"><label>Select bay<select value={selected} onChange={e => setSelected(e.target.value)}><option value="">Choose a bay</option>{slots.map(s => <option key={s.localId} value={s.localId}>{s.slotNumber}</option>)}</select></label><span>{slots.length} bays on this floor</span></div>
    {selectedSlot && <div className="admin-form-grid"><label>Bay number<input value={selectedSlot.slotNumber} onChange={e => changeSlot(selected, { slotNumber: e.target.value })} /></label><label>Type<select value={selectedSlot.type} onChange={e => changeSlot(selected, { type: e.target.value })}>{types.map(t => <option key={t}>{t}</option>)}</select></label>{(['canvasX', 'canvasY', 'canvasWidth', 'canvasHeight'] as const).map(key => <label key={key}>{key.replace('canvas', '')}<input type="number" value={selectedSlot[key] ?? 0} onChange={e => changeSlot(selected, { [key]: Number(e.target.value) })} /></label>)}<label>Sensor ID (UUID)<input value={selectedSlot.assignedSensorId || ''} onChange={e => changeSlot(selected, { assignedSensorId: e.target.value })} /></label><label>Camera ID (UUID)<input value={selectedSlot.assignedCameraId || ''} onChange={e => changeSlot(selected, { assignedCameraId: e.target.value })} /></label><label>Nearest waypoint<select value={selectedSlot.nearestWaypointId || ''} onChange={e => changeSlot(selected, { nearestWaypointId: e.target.value })}><option value="">None</option>{waypoints.map(w => <option key={w.id}>{w.id}</option>)}</select></label><button className="btn btn-secondary" onClick={() => { setSlots(current => current.filter(s => s.localId !== selected)); setSelected(''); setDirty(true); }}>Remove bay from draft</button></div>}
    <h3>Navigation waypoints</h3><p className="muted">Enter neighbor IDs separated by commas to connect paths.</p>{waypoints.map(w => <div key={w.id} className="admin-toolbar"><strong>{w.id}</strong><label>X<input type="number" min={0} max={1000} value={w.x} onChange={e => { setWaypoints(current => current.map(p => p.id === w.id ? { ...p, x: Number(e.target.value) } : p)); setDirty(true); }} /></label><label>Y<input type="number" min={0} max={600} value={w.y} onChange={e => { setWaypoints(current => current.map(p => p.id === w.id ? { ...p, y: Number(e.target.value) } : p)); setDirty(true); }} /></label><label>Neighbors<input value={w.neighbors.join(',')} onChange={e => { setWaypoints(current => current.map(p => p.id === w.id ? { ...p, neighbors: e.target.value.split(',').map(id => id.trim()).filter(Boolean) } : p)); setDirty(true); }} /></label><button className="btn btn-secondary" onClick={() => { setWaypoints(current => current.filter(p => p.id !== w.id).map(p => ({ ...p, neighbors: p.neighbors.filter(id => id !== w.id) }))); setDirty(true); }}>Remove waypoint</button></div>)}</>}
    </div>
  </>;
}
