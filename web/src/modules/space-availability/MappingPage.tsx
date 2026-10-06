import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { apiClient } from '../../lib/apiClient';
import { allZones, getData, postData, messageOf, type FloorPlan, type Slot, type Zone } from '../../services/adminService';
import { Loading, Notice, PageHeading } from '../../components/PageTools';
import { Copy, Grid3x3, Maximize, MousePointer2, Redo2, RotateCcw, Route, Save, SquarePlus, Trash2, Undo2, ZoomIn, ZoomOut } from 'lucide-react';
import { moveWithinFloor, resizeWithinFloor } from './mappingGeometry';

interface EditorSlot extends Slot { localId: string; isNew?: boolean }
interface Waypoint { id: string; x: number; y: number; neighbors: string[] }
const types = ['Standard', 'Compact', 'Large', 'EV', 'Accessible'];
const color: Record<string, string> = { Available: 'var(--good)', Reserved: 'var(--warn)', Occupied: 'var(--crit)', Maintenance: 'var(--ink-3)' };
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
  const [grid, setGrid] = useState({ rows: 4, cols: 5, width: 80, height: 55, gap: 12, prefix: 'Space', type: 'Standard' });
  const zone = zones.find(z => z.id === zoneId);
  type Snapshot = { slots: EditorSlot[]; waypoints: Waypoint[]; image: string; floorName: string };
  const [past, setPast] = useState<Snapshot[]>([]);
  const [future, setFuture] = useState<Snapshot[]>([]);
  const [snap, setSnap] = useState(true);
  const [zoom, setZoom] = useState(1);
  const [view, setView] = useState({ x: 0, y: 0 });
  const [recovery, setRecovery] = useState<Snapshot | null>(null);
  const [draftWarning, setDraftWarning] = useState('');
  const loadedDraftKey = useRef('');
  const draftKey = `parking-layout-draft:${zoneId}:${floor}`;
  const svgRef = useRef<SVGSVGElement>(null);
  const gestureChanged = useRef(false);
  const gesture = useRef<{ id: string; kind: 'move' | 'resize' | 'waypoint' | 'pan'; x: number; y: number; startX: number; startY: number; width: number; height: number } | null>(null);
  const snapshot = (): Snapshot => structuredClone({ slots, waypoints, image, floorName });
  const checkpoint = () => { setPast(current => [...current.slice(-49), snapshot()]); setFuture([]); };
  const restore = (value: Snapshot) => { setSlots(value.slots); setWaypoints(value.waypoints); setImage(value.image); setFloorName(value.floorName); setDirty(true); setSelected(''); };
  const undo = () => { const value = past[past.length - 1]; if (!value) return; setFuture(current => [...current, snapshot()]); setPast(current => current.slice(0, -1)); restore(value); };
  const redo = () => { const value = future[future.length - 1]; if (!value) return; setPast(current => [...current, snapshot()]); setFuture(current => current.slice(0, -1)); restore(value); };
  const coordinate = (event: { clientX: number; clientY: number }) => {
    const matrix = svgRef.current?.getScreenCTM();
    return matrix ? new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse()) : { x: 0, y: 0 };
  };
  const quantize = (value: number) => snap ? Math.round(value / 10) * 10 : Math.round(value);
  const beginGesture = (event: ReactPointerEvent<SVGElement>, id: string, kind: 'move' | 'resize' | 'waypoint') => {
    event.stopPropagation(); if (busy || tool !== 'select') return;
    const point = coordinate(event), slot = slots.find(s => s.localId === id), waypoint = waypoints.find(w => w.id === id);
    setSelected(id); gestureChanged.current = false;
    gesture.current = { id, kind, x: point.x, y: point.y, startX: slot?.canvasX ?? waypoint?.x ?? 0, startY: slot?.canvasY ?? waypoint?.y ?? 0, width: slot?.canvasWidth ?? 80, height: slot?.canvasHeight ?? 55 };
    svgRef.current?.setPointerCapture(event.pointerId);
  };
  const moveGesture = (event: ReactPointerEvent<SVGSVGElement>) => {
    const drag = gesture.current; if (!drag || busy) return;
    if (drag.kind === 'pan') { const rect = event.currentTarget.getBoundingClientRect(); setView({ x: drag.startX - (event.clientX - drag.x) / rect.width * 1000 / zoom, y: drag.startY - (event.clientY - drag.y) / rect.height * 600 / zoom }); return; }
    const point = coordinate(event), dx = point.x - drag.x, dy = point.y - drag.y;
    if (Math.abs(dx) + Math.abs(dy) < 2 && !gestureChanged.current) return;
    if (!gestureChanged.current) { checkpoint(); gestureChanged.current = true; }
    if (drag.kind === 'waypoint') setWaypoints(current => current.map(w => w.id === drag.id ? { ...w, x: Math.max(0, Math.min(1000, quantize(drag.startX + dx))), y: Math.max(0, Math.min(600, quantize(drag.startY + dy))) } : w));
    else setSlots(current => current.map(s => {
      if (s.localId !== drag.id) return s;
      if (drag.kind === 'resize') { const size = resizeWithinFloor(drag.startX, drag.startY, drag.width + dx, drag.height + dy, snap); return { ...s, canvasWidth: size.width, canvasHeight: size.height }; }
      const position = moveWithinFloor(drag.startX + dx, drag.startY + dy, drag.width, drag.height, snap);
      return { ...s, canvasX: position.x, canvasY: position.y };
    }));
    setDirty(true);
  };
  const removeSelected = () => { checkpoint(); setSlots(current => current.filter(s => s.localId !== selected).map(s => s.nearestWaypointId === selected ? { ...s, nearestWaypointId: '' } : s)); setWaypoints(current => current.filter(w => w.id !== selected).map(w => ({ ...w, neighbors: w.neighbors.filter(id => id !== selected) }))); setSelected(''); setDirty(true); };
  const duplicate = () => { const slot = slots.find(s => s.localId === selected); if (!slot) return; checkpoint(); const id = `new-${crypto.randomUUID()}`; let n = 1; while (slots.some(s => s.slotNumber === `${slot.slotNumber}-${n}`)) n++; setSlots(current => [...current, { ...slot, id: '', localId: id, isNew: true, slotNumber: `${slot.slotNumber}-${n}`, status: 'Available', assignedSensorId: '', assignedCameraId: '', canvasX: Math.min(1000 - (slot.canvasWidth ?? 80), (slot.canvasX ?? 0) + 20), canvasY: Math.min(600 - (slot.canvasHeight ?? 55), (slot.canvasY ?? 0) + 20) }]); setSelected(id); setDirty(true); };
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
      loadedDraftKey.current = `parking-layout-draft:${zoneId}:${floor}`;
      try { const raw = sessionStorage.getItem(loadedDraftKey.current); const value = raw ? JSON.parse(raw) as Snapshot : null; setRecovery(value && Array.isArray(value.slots) && Array.isArray(value.waypoints) && typeof value.image === 'string' && typeof value.floorName === 'string' ? value : null); } catch { setRecovery(null); }
      setPlans(floors);
      const plan = floors.find(f => f.floorOrder === floor);
      setImage(plan?.imageUrl || ''); setFloorName(plan?.floorName || `Floor ${floor}`);
      let graph: Waypoint[] = [];
      try { graph = JSON.parse(plan?.waypointGraphJson || '[]'); if (!Array.isArray(graph)) graph = []; } catch { graph = []; }
      setWaypoints(graph);
      setSlots(detail.slots.filter(s => s.floor === floor).map((s, index) => ({ ...s, localId: s.id, canvasX: s.canvasX ?? 40 + (index % 10) * 92, canvasY: s.canvasY ?? 50 + Math.floor(index / 10) * 80, canvasWidth: s.canvasWidth ?? 80, canvasHeight: s.canvasHeight ?? 55 })));
      setDirty(false); setPast([]); setFuture([]); setZoom(1); setView({ x: 0, y: 0 });
    }).catch(err => { if (!controller.signal.aborted) setError(messageOf(err)); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [zoneId, floor, revision]);
  useEffect(() => {
    if (!dirty || loading || busy || loadedDraftKey.current !== draftKey) return;
    const timer = window.setTimeout(() => {
      try { sessionStorage.setItem(draftKey, JSON.stringify({ slots, waypoints, image, floorName })); setDraftWarning(''); }
      catch { setDraftWarning('Draft recovery is unavailable. Save your layout before closing this tab.'); }
    }, 300);
    return () => window.clearTimeout(timer);
  }, [dirty, loading, busy, draftKey, slots, waypoints, image, floorName]);
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
  const discard = () => { if (dirty && !window.confirm('Discard unsaved layout changes?')) return false; try { sessionStorage.removeItem(draftKey); } catch { /* Storage may be unavailable. */ } setRecovery(null); return true; };
  const changeSlot = (id: string, changes: Partial<EditorSlot>) => { checkpoint(); setSlots(current => current.map(slot => slot.localId === id ? { ...slot, ...changes } : slot)); setDirty(true); };
  const addSlot = (x = 40, y = 40) => { checkpoint();
    const id = `new-${crypto.randomUUID()}`;
    let n = slots.length + 1;
    while (slots.some(s => s.slotNumber === `F${floor}-${n}`)) n++;
    setSlots(current => [...current, { id: '', localId: id, isNew: true, slotNumber: `F${floor}-${n}`, floor, type: 'Standard', status: 'Available', canvasX: x, canvasY: y, canvasWidth: 80, canvasHeight: 55 }]); setSelected(id); setDirty(true);
  };
  const addWaypoint = (x = 50, y = 50) => { checkpoint(); setWaypoints(current => [...current, { id: `wp-${crypto.randomUUID().slice(0, 8)}`, x, y, neighbors: [] }]); setDirty(true); };
  const save = async () => {
    setBusy(true); setError(''); setMessage('');
    try {
      if (!validImage(image)) throw new Error('Use an http(s) image URL or upload an image.');
      if (slots.some(s => !s.slotNumber.trim()) || new Set(slots.map(s => s.slotNumber.trim())).size !== slots.length) throw new Error('Each parking space needs a unique label.');
      if (slots.some(s => ![s.canvasX, s.canvasY, s.canvasWidth, s.canvasHeight].every(Number.isFinite) || (s.canvasX ?? 0) < 0 || (s.canvasY ?? 0) < 0 || (s.canvasWidth ?? 0) < 30 || (s.canvasHeight ?? 0) < 30 || (s.canvasX ?? 0) + (s.canvasWidth ?? 0) > 1000 || (s.canvasY ?? 0) + (s.canvasHeight ?? 0) > 600)) throw new Error('Keep spaces inside the canvas with a minimum size of 30 × 30.');
      if (!floorName.trim()) throw new Error('Enter a floor name.');
      const waypointIds = new Set(waypoints.map(w => w.id));
      if (waypoints.some(w => !Number.isFinite(w.x) || !Number.isFinite(w.y) || w.x < 0 || w.x > 1000 || w.y < 0 || w.y > 600 || w.neighbors.some(id => !waypointIds.has(id.trim()))))
        throw new Error('Waypoint positions must be inside the canvas and neighbors must refer to existing waypoint IDs.');
      if (slots.some(s => s.nearestWaypointId && !waypointIds.has(s.nearestWaypointId))) throw new Error('A bay refers to a removed waypoint. Select a new nearest waypoint.');
      const plan = await postData<{ id: string }>(`/api/zones/${zoneId}/floor-plans`, { floorName, floorOrder: floor, imageUrl: image, imageWidthPx: 1000, imageHeightPx: 600, waypointGraphJson: JSON.stringify(waypoints) });
      try {
        await apiClient.put(`/api/admin/zones/${zoneId}/floors/${floor}/slots`, { floorPlanId: plan.id, slots: slots.map(slot => ({ ...slot, id: slot.isNew ? null : slot.id, assignedSensorId: slot.assignedSensorId || null, assignedCameraId: slot.assignedCameraId || null })) });
      } catch (err) { throw new Error(`Floor plan saved, but bays were not saved: ${messageOf(err)}. Your bay edits are still available; correct them and retry.`); }
      try { sessionStorage.removeItem(draftKey); } catch { /* Saving to the server succeeded. */ }
      setRecovery(null); setDirty(false); setMessage('Floor plan and bays saved.'); setRevision(n => n + 1);
    } catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  const upload = (file?: File) => {
    if (!file) return;
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || file.size > 3 * 1024 * 1024) { setError('Choose a PNG, JPEG, or WebP image under 3 MB.'); return; }
    const reader = new FileReader(); reader.onload = () => { checkpoint(); setImage(String(reader.result)); setDirty(true); }; reader.readAsDataURL(file);
  };
  const detect = async () => {
    if (!zone) return; setBusy(true); setError(''); setMessage('');
    try {
      if (![grid.rows, grid.cols, grid.width, grid.height, grid.gap].every(Number.isFinite) || !Number.isInteger(grid.rows) || !Number.isInteger(grid.cols) || grid.rows < 1 || grid.cols < 1 || grid.rows * grid.cols > 100 || grid.width < 30 || grid.height < 30 || grid.gap < 0 || 40 + grid.cols * grid.width + (grid.cols - 1) * grid.gap > 1000 || 50 + grid.rows * grid.height + (grid.rows - 1) * grid.gap > 600 || !grid.prefix.trim()) throw new Error('Choose a grid of up to 100 spaces that fits inside the floor. Use sizes of at least 30 and a label prefix.');
      const result = await postData<{ detected_slots: typeof detections; algorithm: string }>('/api/admin/ai/slot-detection', { north: zone.latitude + .0006, south: zone.latitude - .0006, east: zone.longitude + .0009, west: zone.longitude - .0009, cols: grid.cols, rows: grid.rows, slot_prefix: `${grid.prefix}-F${floor}` });
      setDetections(result.detected_slots); setMessage(`${result.detected_slots.length} proposed bays. Method: ${result.algorithm}. Review before adding.`);
    } catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  const importDetections = () => { checkpoint();
    setSlots(current => [...current, ...detections.filter(d => !current.some(s => s.slotNumber === d.slot_code)).map((d, index) => ({ id: '', localId: `new-${crypto.randomUUID()}`, isNew: true, slotNumber: d.slot_code, type: grid.type, status: 'Available', floor, boundingBoxJson: JSON.stringify(d.bounds), canvasX: 40 + (index % grid.cols) * (grid.width + grid.gap), canvasY: 50 + Math.floor(index / grid.cols) * (grid.height + grid.gap), canvasWidth: grid.width, canvasHeight: grid.height }))]);
    setDirty(true); setDetections([]); setMessage('Proposals added to your draft. Adjust their positions and save.');
  };
  const selectedSlot = slots.find(s => s.localId === selected);

  const selectedPoint = waypoints.find(w => w.id === selected);
  const isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform);
  const mod = isMac ? '⌘' : 'Ctrl';
  return <><PageHeading title="Slot Mapping" description="Place parking spaces, draw indoor paths and save each floor layout."><span className={dirty ? 'badge tone-warn' : 'badge tone-good'} role="status">{dirty ? 'Unsaved changes' : 'All changes saved'}</span><button className="btn btn-primary" disabled={loading || busy || !zoneId || !dirty} onClick={() => void save()}><Save size={16} aria-hidden="true" />{busy ? 'Saving…' : 'Save layout'}</button></PageHeading><Notice error={error} message={message} />{draftWarning && <div className="notice tone-warn" role="status"><div>{draftWarning}</div></div>}{recovery && <div className="notice tone-info"><RotateCcw size={18} aria-hidden="true" /><div>An unsaved draft is available for this floor.</div><div className="row" style={{ flex: 'none' }}><button className="btn btn-primary btn-sm" disabled={busy || loading} onClick={() => { checkpoint(); restore(recovery); setRecovery(null); }}>Restore draft</button><button className="btn btn-secondary btn-sm" disabled={busy || loading} onClick={() => { try { sessionStorage.removeItem(draftKey); } catch { /* Storage unavailable. */ } setRecovery(null); }}>Discard</button></div></div>}
    <div className="card mapping-editor" onKeyDown={event => {
      if ((event.target as HTMLElement).closest('input, select, textarea') || busy) return;
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') { event.preventDefault(); if (event.shiftKey) redo(); else undo(); }
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'd') { event.preventDefault(); duplicate(); }
      if (event.key === 'Delete' && selected) { event.preventDefault(); removeSelected(); }
      if (event.key === 'Escape') { setTool('select'); setSelected(''); }
    }}>
      <div className="mapping-context">
        <label className="field"><span className="field-label">Parking location</span><select value={zoneId} disabled={busy} onChange={e => { if (discard()) { setZoneId(e.target.value); setFloor(0); } }}>{zones.map(z => <option key={z.id} value={z.id}>{z.name}</option>)}</select></label>
        <label className="field narrow"><span className="field-label">Floor</span><input type="number" value={floor} disabled={busy} onChange={e => { if (discard()) setFloor(Number(e.target.value)); }} /></label>
        <button className="btn btn-secondary" disabled={busy} onClick={() => { if (discard()) setRevision(n => n + 1); }}><RotateCcw size={16} aria-hidden="true" />Reload</button>
      </div>
      {loading ? <Loading label="Loading layout…" /> : !zoneId ? <p style={{ padding: 20 }}>Create a parking location before mapping spaces.</p> : <fieldset className="mapping-workspace" disabled={busy}>
        <section className="mapping-stage" aria-label="Layout workspace">
          <div className="mapping-tools">
            <div className="segmented" role="group" aria-label="Editing tool">{(['select', 'slot', 'waypoint'] as const).map(mode => { const Icon = mode === 'select' ? MousePointer2 : mode === 'slot' ? SquarePlus : Route; return <button key={mode} type="button" aria-pressed={tool === mode} onClick={() => setTool(mode)}><Icon size={15} aria-hidden="true" />{mode === 'select' ? 'Select / move' : mode === 'slot' ? 'Add space' : 'Add path point'}</button>; })}</div>
            <div className="row" style={{ gap: 4 }}><button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Undo" title={`Undo (${mod}+Z)`} disabled={!past.length} onClick={undo}><Undo2 size={16} /></button><button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Redo" title={`Redo (${mod}+Shift+Z)`} disabled={!future.length} onClick={redo}><Redo2 size={16} /></button></div>
            <span style={{ flex: 1 }} />
            <div className="row" style={{ gap: 4 }}><button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Zoom out" disabled={zoom <= 1} onClick={() => setZoom(z => Math.max(1, z - .25))}><ZoomOut size={16} /></button><span className="subtle tabular" style={{ minWidth: 40, textAlign: 'center' }}>{Math.round(zoom * 100)}%</span><button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Zoom in" disabled={zoom >= 3} onClick={() => setZoom(z => Math.min(3, z + .25))}><ZoomIn size={16} /></button><button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Fit floor" title="Fit floor" onClick={() => { setZoom(1); setView({ x: 0, y: 0 }); }}><Maximize size={16} /></button></div>
            <label className="check"><input type="checkbox" checked={snap} onChange={e => setSnap(e.target.checked)} />Snap to grid</label>
          </div>
          <p className="mapping-hint">{tool === 'slot' ? 'Click an empty area to place a parking space.' : tool === 'waypoint' ? 'Click along an entrance or aisle to add a path point.' : 'Drag a space or path point to move it. Drag the corner handle to resize a selected space.'}</p>
          <svg ref={svgRef} className="mapping-canvas" viewBox={[view.x, view.y, 1000 / zoom, 600 / zoom].join(' ')} tabIndex={0} role="img" aria-label="Interactive floor plan. Properties are available in the side panel. Shift-drag empty space to pan."
            onPointerDown={event => { if (busy) return; event.currentTarget.focus(); if (event.shiftKey || event.button === 1) { gesture.current = { id: '', kind: 'pan', x: event.clientX, y: event.clientY, startX: view.x, startY: view.y, width: 0, height: 0 }; event.currentTarget.setPointerCapture(event.pointerId); return; } const point = coordinate(event); if (tool === 'slot') addSlot(Math.max(0, Math.min(920, quantize(point.x))), Math.max(0, Math.min(545, quantize(point.y)))); else if (tool === 'waypoint') addWaypoint(Math.max(0, Math.min(1000, quantize(point.x))), Math.max(0, Math.min(600, quantize(point.y)))); else setSelected(''); }}
            onPointerMove={moveGesture} onPointerUp={event => { gesture.current = null; if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId); }} onPointerCancel={() => { gesture.current = null; }}>
            <defs><pattern id="mapping-grid" width="20" height="20" patternUnits="userSpaceOnUse"><path d="M 20 0 L 0 0 0 20" fill="none" style={{ stroke: 'var(--line)' }} strokeWidth=".6" /></pattern></defs><rect width="1000" height="600" fill="url(#mapping-grid)" />{image && validImage(image) && <image href={image} width="1000" height="600" opacity=".65" preserveAspectRatio="none" />}
            {waypoints.flatMap(w => w.neighbors.map(id => { const target = waypoints.find(p => p.id === id); return target ? <line key={w.id + '-' + id} x1={w.x} y1={w.y} x2={target.x} y2={target.y} style={{ stroke: 'var(--info)' }} strokeWidth="3" strokeLinecap="round" pointerEvents="none" /> : null; }))}
            {detections.map((d, i) => <g key={d.slot_code} pointerEvents="none"><rect x={40 + (i % grid.cols) * (grid.width + grid.gap)} y={50 + Math.floor(i / grid.cols) * (grid.height + grid.gap)} width={grid.width} height={grid.height} rx="5" style={{ fill: 'var(--info-soft)', stroke: 'var(--info)' }} strokeDasharray="5 3" /><text x={48 + (i % grid.cols) * (grid.width + grid.gap)} y={70 + Math.floor(i / grid.cols) * (grid.height + grid.gap)} fontSize="10" style={{ fill: 'var(--info-ink)' }}>{d.slot_code}</text></g>)}
            {slots.map(slot => { const x = slot.canvasX ?? 0, y = slot.canvasY ?? 0, w = slot.canvasWidth ?? 80, h = slot.canvasHeight ?? 55, active = selected === slot.localId; return <g key={slot.localId} className="mapping-object" onPointerDown={e => beginGesture(e, slot.localId, 'move')}>
              <rect x={x} y={y} width={w} height={h} rx="6" style={{ fill: 'var(--surface)', stroke: active ? 'var(--ink)' : color[slot.status] || 'var(--ink-3)' }} strokeWidth={active ? 3 : 1.5} />
              <rect x={x + 1} y={y + 1} width={Math.max(0, w - 2)} height="5" rx="2" style={{ fill: color[slot.status] || 'var(--ink-3)' }} pointerEvents="none" />
              <text x={x + 8} y={y + 24} fontSize="12" fontWeight="700" style={{ fill: 'var(--ink)' }} pointerEvents="none">{slot.slotNumber}</text>
              <text x={x + 8} y={y + 40} fontSize="9" style={{ fill: 'var(--ink-2)' }} pointerEvents="none">{slot.type}</text>
              {active && <rect className="mapping-resize" x={x + w - 6} y={y + h - 6} width="12" height="12" rx="2" style={{ fill: 'var(--surface)', stroke: 'var(--ink)' }} strokeWidth="2" onPointerDown={e => beginGesture(e, slot.localId, 'resize')} />}
            </g>; })}
            {waypoints.map((w, index) => <g key={w.id} className="mapping-object" onPointerDown={e => beginGesture(e, w.id, 'waypoint')}><circle cx={w.x} cy={w.y} r={selected === w.id ? 10 : 7} style={{ fill: 'var(--info)', stroke: selected === w.id ? 'var(--ink)' : 'var(--surface)' }} strokeWidth="2" /><text x={w.x + 12} y={w.y + 4} fontSize="11" fontWeight="600" style={{ fill: 'var(--ink-2)' }}>Point {index + 1}</text></g>)}
          </svg>
          <div className="mapping-footer"><span>{slots.length} spaces · {waypoints.length} path points</span><div className="legend">{Object.entries(color).map(([label, fill]) => <span key={label}><i className="dot" style={{ background: fill }} />{label}</span>)}<span><i className="dot info" style={{ borderRadius: '50%' }} />Path point</span></div></div>
          {detections.length > 0 && <div className="mapping-proposals"><h3>Review proposed spaces</h3><p>This is a generated grid. Adjust the spaces to match your blueprint after adding them.</p><p className="mono" style={{ fontSize: '.75rem' }}>{detections.map(d => d.slot_code).join(' · ')}</p><div className="row"><button type="button" className="btn btn-primary btn-sm" onClick={importDetections}>Add to layout</button><button type="button" className="btn btn-secondary btn-sm" onClick={() => setDetections([])}>Discard</button></div></div>}
        </section>
        <aside className="mapping-properties" aria-label="Layout properties">
          <label className="field"><span className="field-label">Selected item</span><select value={selected} onChange={e => { setSelected(e.target.value); setTool('select'); }}><option value="">Choose an item</option><optgroup label="Parking spaces">{slots.map(s => <option key={s.localId} value={s.localId}>{s.slotNumber}</option>)}</optgroup><optgroup label="Path points">{waypoints.map((w, i) => <option key={w.id} value={w.id}>Point {i + 1}</option>)}</optgroup></select></label>
          {!selectedSlot && !selectedPoint && <div className="stack-sm"><h3>Build your floor layout</h3><p className="muted">Upload a blueprint in Floor settings, then add parking spaces on the canvas. Select an item to edit its details.</p><div className="row"><button type="button" className="btn btn-secondary btn-sm" onClick={() => addSlot()}><SquarePlus size={14} aria-hidden="true" />Add a space</button><button type="button" className="btn btn-secondary btn-sm" onClick={() => addWaypoint()}><Route size={14} aria-hidden="true" />Add a path point</button></div></div>}
          {selectedSlot && <div className="stack-sm"><h3>Parking space</h3><label className="field"><span className="field-label">Space label</span><input value={selectedSlot.slotNumber} onChange={e => changeSlot(selected, { slotNumber: e.target.value })} /></label>{slots.some(s => s.localId !== selected && s.slotNumber.trim() === selectedSlot.slotNumber.trim()) && <p className="field-error" role="alert">This label is already in use.</p>}<label className="field"><span className="field-label">Space type</span><select value={selectedSlot.type} onChange={e => changeSlot(selected, { type: e.target.value })}>{types.map(t => <option key={t}>{t}</option>)}</select></label><div className="row"><button type="button" className="btn btn-secondary btn-sm" onClick={duplicate}><Copy size={14} aria-hidden="true" />Duplicate</button><button type="button" className="btn btn-danger btn-sm" onClick={removeSelected}><Trash2 size={14} aria-hidden="true" />Remove</button></div>
            <details className="disclosure"><summary>Position and size</summary><div><p className="subtle">Canvas units: 1000 wide × 600 high.</p><div className="mapping-numbers">{(['canvasX', 'canvasY', 'canvasWidth', 'canvasHeight'] as const).map(key => <label key={key} className="field"><span className="field-label">{key === 'canvasX' ? 'Position X' : key === 'canvasY' ? 'Position Y' : key.replace('canvas', '')}</span><input type="number" min={key === 'canvasX' || key === 'canvasY' ? 0 : 30} value={selectedSlot[key] ?? 0} onChange={e => changeSlot(selected, { [key]: Number(e.target.value) })} /></label>)}</div></div></details>
            <details className="disclosure"><summary>Device assignments</summary><div><p className="subtle">Link devices using their registered IDs. Leave blank while designing.</p><label className="field"><span className="field-label">Sensor ID (UUID)</span><input value={selectedSlot.assignedSensorId || ''} onChange={e => changeSlot(selected, { assignedSensorId: e.target.value })} /></label><label className="field"><span className="field-label">Camera ID (UUID)</span><input value={selectedSlot.assignedCameraId || ''} onChange={e => changeSlot(selected, { assignedCameraId: e.target.value })} /></label></div></details>
            <details className="disclosure"><summary>Navigation connection</summary><div><label className="field"><span className="field-label">Closest path point</span><select value={selectedSlot.nearestWaypointId || ''} onChange={e => changeSlot(selected, { nearestWaypointId: e.target.value })}><option value="">None</option>{waypoints.map((w, i) => <option key={w.id} value={w.id}>Point {i + 1}</option>)}</select></label></div></details></div>}
          {selectedPoint && <div className="stack-sm"><h3>Path point {waypoints.indexOf(selectedPoint) + 1}</h3><p className="muted">Connect points along entrances and aisles. Blue lines show the connections.</p><div className="mapping-numbers">{(['x', 'y'] as const).map(axis => <label key={axis} className="field"><span className="field-label">Position {axis.toUpperCase()}</span><input type="number" min={0} max={axis === 'x' ? 1000 : 600} value={selectedPoint[axis]} onChange={e => { checkpoint(); setWaypoints(current => current.map(w => w.id === selected ? { ...w, [axis]: Number(e.target.value) } : w)); setDirty(true); }} /></label>)}</div><h3 style={{ marginTop: 6 }}>Connected path points</h3>{waypoints.filter(w => w.id !== selected).map(w => <label key={w.id} className="check"><input type="checkbox" checked={selectedPoint.neighbors.includes(w.id)} onChange={e => { checkpoint(); setWaypoints(current => current.map(p => p.id === selected ? { ...p, neighbors: e.target.checked ? [...p.neighbors, w.id] : p.neighbors.filter(id => id !== w.id) } : p.id === w.id ? { ...p, neighbors: e.target.checked ? [...new Set([...p.neighbors, selected])] : p.neighbors.filter(id => id !== selected) } : p)); setDirty(true); }} />Point {waypoints.indexOf(w) + 1}</label>)}{waypoints.length < 2 && <p className="subtle">Add another point to connect a path.</p>}<details className="disclosure"><summary>Advanced connection IDs</summary><div><p className="subtle mono">ID: {selectedPoint.id}</p><label className="field"><span className="field-label">Connected IDs, comma-separated</span><input value={selectedPoint.neighbors.join(',')} onChange={e => { checkpoint(); setWaypoints(current => current.map(w => w.id === selected ? { ...w, neighbors: e.target.value.split(',').map(id => id.trim()).filter(Boolean) } : w)); setDirty(true); }} /></label></div></details><div><button type="button" className="btn btn-danger btn-sm" onClick={removeSelected}><Trash2 size={14} aria-hidden="true" />Remove path point</button></div></div>}
          <div>
            <details className="disclosure"><summary>Floor settings</summary><div><p className="subtle">Existing floors: {plans.map(f => f.floorName + ' (' + f.floorOrder + ')').join(', ') || 'None yet'}. Enter a new floor number above to create a floor.</p><label className="field"><span className="field-label">Floor name</span><input value={floorName} onChange={e => { checkpoint(); setFloorName(e.target.value); setDirty(true); }} /></label><label className="field"><span className="field-label">Blueprint image URL</span><input value={image.startsWith('data:') ? '' : image} placeholder={image.startsWith('data:') ? 'Uploaded image selected' : 'https://…'} onChange={e => { checkpoint(); setImage(e.target.value); setDirty(true); }} /></label><label className="field"><span className="field-label">Upload blueprint</span><input type="file" accept="image/png,image/jpeg,image/webp" onChange={e => upload(e.target.files?.[0])} /><span className="field-hint">PNG, JPEG or WebP, up to 3 MB.</span></label></div></details>
            <details className="disclosure"><summary><span className="row" style={{ gap: 8 }}><Grid3x3 size={15} aria-hidden="true" />Generate a grid</span></summary><div><p className="subtle">Choose the grid dimensions, preview it, then add the spaces. This does not detect spaces from the blueprint.</p><div className="mapping-numbers">{(['rows', 'cols', 'width', 'height', 'gap'] as const).map(key => <label key={key} className="field"><span className="field-label">{key === 'cols' ? 'Per row' : key === 'gap' ? 'Spacing' : key.charAt(0).toUpperCase() + key.slice(1)}</span><input type="number" min={key === 'gap' ? 0 : key === 'width' || key === 'height' ? 30 : 1} value={grid[key]} disabled={detections.length > 0} onChange={e => setGrid(current => ({ ...current, [key]: Number(e.target.value) }))} /></label>)}</div><label className="field"><span className="field-label">Label prefix</span><input value={grid.prefix} disabled={detections.length > 0} onChange={e => setGrid(current => ({ ...current, prefix: e.target.value }))} /></label><label className="field"><span className="field-label">Space type</span><select value={grid.type} disabled={detections.length > 0} onChange={e => setGrid(current => ({ ...current, type: e.target.value }))}>{types.map(t => <option key={t}>{t}</option>)}</select></label><button type="button" className="btn btn-secondary" disabled={busy} onClick={() => void detect()}>Preview generated grid</button></div></details>
            <details className="disclosure"><summary>Keyboard shortcuts</summary><div><div className="shortcuts"><span><kbd className="kbd">{mod}</kbd> <kbd className="kbd">Z</kbd></span><span>Undo</span><span><kbd className="kbd">{mod}</kbd> <kbd className="kbd">⇧</kbd> <kbd className="kbd">Z</kbd></span><span>Redo</span><span><kbd className="kbd">{mod}</kbd> <kbd className="kbd">D</kbd></span><span>Duplicate space</span><span><kbd className="kbd">Del</kbd></span><span>Remove selection</span><span><kbd className="kbd">Esc</kbd></span><span>Back to Select</span><span><kbd className="kbd">⇧</kbd> + drag</span><span>Pan the canvas</span></div></div></details>
          </div>
        </aside>
      </fieldset>}
    </div>
  </>;
}
