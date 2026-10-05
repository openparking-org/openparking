import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { apiClient } from '../../lib/apiClient';
import { allZones, getData, postData, messageOf, type FloorPlan, type Slot, type Zone } from '../../services/adminService';
import { Notice, PageHeading } from '../../components/PageTools';
import { moveWithinFloor, resizeWithinFloor } from './mappingGeometry';

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
      if (slots.some(s => ![s.canvasX, s.canvasY, s.canvasWidth, s.canvasHeight].every(Number.isFinite) || (s.canvasX ?? 0) < 0 || (s.canvasY ?? 0) < 0 || (s.canvasWidth ?? 0) < 30 || (s.canvasHeight ?? 0) < 30 || (s.canvasX ?? 0) + (s.canvasWidth ?? 0) > 1000 || (s.canvasY ?? 0) + (s.canvasHeight ?? 0) > 600)) throw new Error('Keep spaces inside the canvas with a minimum size of 30 � 30.');
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
  return <><PageHeading title="Slot Mapping" description="Place parking spaces, draw indoor paths, and save your floor layout."><button className="btn btn-primary" disabled={loading || busy || !zoneId || !dirty} onClick={() => void save()}>{busy ? 'Saving�' : 'Save layout'}</button></PageHeading><Notice error={error} message={message} /><Notice message={draftWarning} />{recovery && <div className="admin-notice"><p>An unsaved draft is available for this floor.</p><button className="btn btn-primary" disabled={busy || loading} onClick={() => { checkpoint(); restore(recovery); setRecovery(null); }}>Restore draft</button><button className="btn btn-secondary" disabled={busy || loading} onClick={() => { try { sessionStorage.removeItem(draftKey); } catch { /* Storage unavailable. */ } setRecovery(null); }}>Discard draft</button></div>}
    <div className="glass-panel admin-panel mapping-editor" onKeyDown={event => {
      if ((event.target as HTMLElement).closest('input, select, textarea') || busy) return;
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') { event.preventDefault(); if (event.shiftKey) redo(); else undo(); }
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'd') { event.preventDefault(); duplicate(); }
      if (event.key === 'Delete' && selected) { event.preventDefault(); removeSelected(); }
      if (event.key === 'Escape') { setTool('select'); setSelected(''); }
    }}>
      <div className="admin-toolbar mapping-context"><label>Parking location<select value={zoneId} disabled={busy} onChange={e => { if (discard()) { setZoneId(e.target.value); setFloor(0); } }}>{zones.map(z => <option key={z.id} value={z.id}>{z.name}</option>)}</select></label><label>Floor number<input type="number" value={floor} disabled={busy} onChange={e => { if (discard()) setFloor(Number(e.target.value)); }} /></label><button className="btn btn-secondary" disabled={busy} onClick={() => { if (discard()) setRevision(n => n + 1); }}>Reload</button><span className={dirty ? 'badge badge-warning' : 'badge badge-success'} role="status">{dirty ? 'Unsaved changes' : 'Saved layout'}</span></div>
      {loading ? <p role="status">Loading layout�</p> : !zoneId ? <p>Create a parking location before mapping spaces.</p> : <fieldset className="mapping-workspace" disabled={busy}>
        <section className="mapping-stage" aria-label="Layout workspace">
          <div className="admin-toolbar mapping-tools">{(['select', 'slot', 'waypoint'] as const).map(mode => <button key={mode} className="btn btn-secondary" aria-pressed={tool === mode} onClick={() => setTool(mode)}>{mode === 'select' ? 'Select / move' : mode === 'slot' ? 'Add parking space' : 'Add path point'}</button>)}<button className="btn btn-secondary" disabled={!past.length} onClick={undo}>Undo</button><button className="btn btn-secondary" disabled={!future.length} onClick={redo}>Redo</button></div>
          <p className="mapping-hint">{tool === 'slot' ? 'Click an empty area to place a parking space.' : tool === 'waypoint' ? 'Click along an entrance or aisle to add a path point.' : 'Drag a space or path point to move it. Drag the corner handle to resize a selected space.'}</p>
          <div className="admin-toolbar mapping-view"><button className="btn btn-secondary" aria-label="Zoom out" disabled={zoom <= 1} onClick={() => setZoom(z => Math.max(1, z - .25))}>-</button><span>{Math.round(zoom * 100)}%</span><button className="btn btn-secondary" aria-label="Zoom in" disabled={zoom >= 3} onClick={() => setZoom(z => Math.min(3, z + .25))}>+</button><button className="btn btn-secondary" onClick={() => { setZoom(1); setView({ x: 0, y: 0 }); }}>Fit floor</button><label className="mapping-checkbox"><input type="checkbox" checked={snap} onChange={e => setSnap(e.target.checked)} />Snap to grid</label></div>
          <svg ref={svgRef} className="mapping-canvas" viewBox={[view.x, view.y, 1000 / zoom, 600 / zoom].join(' ')} tabIndex={0} role="img" aria-label="Interactive floor plan. Properties are available in the side panel. Shift-drag empty space to pan."
            onPointerDown={event => { if (busy) return; event.currentTarget.focus(); if (event.shiftKey || event.button === 1) { gesture.current = { id: '', kind: 'pan', x: event.clientX, y: event.clientY, startX: view.x, startY: view.y, width: 0, height: 0 }; event.currentTarget.setPointerCapture(event.pointerId); return; } const point = coordinate(event); if (tool === 'slot') addSlot(Math.max(0, Math.min(920, quantize(point.x))), Math.max(0, Math.min(545, quantize(point.y)))); else if (tool === 'waypoint') addWaypoint(Math.max(0, Math.min(1000, quantize(point.x))), Math.max(0, Math.min(600, quantize(point.y)))); else setSelected(''); }}
            onPointerMove={moveGesture} onPointerUp={event => { gesture.current = null; if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId); }} onPointerCancel={() => { gesture.current = null; }}>
            <defs><pattern id="mapping-grid" width="20" height="20" patternUnits="userSpaceOnUse"><path d="M 20 0 L 0 0 0 20" fill="none" stroke="#cbd5e1" strokeWidth=".5" /></pattern></defs><rect width="1000" height="600" fill="url(#mapping-grid)" />{image && validImage(image) && <image href={image} width="1000" height="600" opacity=".65" preserveAspectRatio="none" />}
            {waypoints.flatMap(w => w.neighbors.map(id => { const target = waypoints.find(p => p.id === id); return target ? <line key={w.id + '-' + id} x1={w.x} y1={w.y} x2={target.x} y2={target.y} stroke="#2563eb" strokeWidth="3" pointerEvents="none" /> : null; }))}
            {detections.map((d, i) => <g key={d.slot_code} pointerEvents="none"><rect x={40 + (i % grid.cols) * (grid.width + grid.gap)} y={50 + Math.floor(i / grid.cols) * (grid.height + grid.gap)} width={grid.width} height={grid.height} fill="#dbeafe" fillOpacity=".8" stroke="#2563eb" strokeDasharray="5 3" /><text x={48 + (i % grid.cols) * (grid.width + grid.gap)} y={70 + Math.floor(i / grid.cols) * (grid.height + grid.gap)} fontSize="10" fill="#1d4ed8">{d.slot_code}</text></g>)}{slots.map(slot => <g key={slot.localId} className="mapping-object" onPointerDown={e => beginGesture(e, slot.localId, 'move')}><rect x={slot.canvasX} y={slot.canvasY} width={slot.canvasWidth} height={slot.canvasHeight} rx="5" fill={color[slot.status] || '#475569'} stroke={selected === slot.localId ? '#2563eb' : '#fff'} strokeWidth={selected === slot.localId ? 4 : 1} /><text x={(slot.canvasX || 0) + 8} y={(slot.canvasY || 0) + 20} fill="white" fontSize="12">{slot.slotNumber}</text><text x={(slot.canvasX || 0) + 8} y={(slot.canvasY || 0) + 38} fill="white" fontSize="9">{slot.type}</text>{selected === slot.localId && <rect className="mapping-resize" x={(slot.canvasX ?? 0) + (slot.canvasWidth ?? 80) - 6} y={(slot.canvasY ?? 0) + (slot.canvasHeight ?? 55) - 6} width="12" height="12" fill="white" stroke="#2563eb" strokeWidth="2" onPointerDown={e => beginGesture(e, slot.localId, 'resize')} />}</g>)}
            {waypoints.map((w, index) => <g key={w.id} className="mapping-object" onPointerDown={e => beginGesture(e, w.id, 'waypoint')}><circle cx={w.x} cy={w.y} r={selected === w.id ? 10 : 7} fill="#2563eb" stroke="white" strokeWidth="2" /><text x={w.x + 12} y={w.y} fontSize="11">Point {index + 1}</text></g>)}
          </svg>
          <div className="mapping-footer"><span>{slots.length} spaces � {waypoints.length} path points</span><span>Shift + drag to pan � Delete to remove</span></div>
          <div className="mapping-legend">{Object.entries(color).map(([label, fill]) => <span key={label}><i style={{ background: fill }} />{label}</span>)}</div>
          {detections.length > 0 && <div className="mapping-proposals"><h3>Review proposed spaces</h3><p>This is a generated grid. Adjust the spaces to match your blueprint after adding them.</p><p>{detections.map(d => d.slot_code).join(' � ')}</p><button className="btn btn-primary" onClick={importDetections}>Add to layout</button><button className="btn btn-secondary" onClick={() => setDetections([])}>Discard</button></div>}
        </section>
        <aside className="mapping-properties" aria-label="Layout properties">
          <details><summary>Floor settings</summary><p className="muted">Existing floors: {plans.map(f => f.floorName + ' (' + f.floorOrder + ')').join(', ') || 'None yet'}. Enter a new floor number above to create a floor.</p><label>Floor name<input value={floorName} onChange={e => { checkpoint(); setFloorName(e.target.value); setDirty(true); }} /></label><label>Blueprint image URL<input value={image.startsWith('data:') ? '' : image} placeholder={image.startsWith('data:') ? 'Uploaded image selected' : 'https://�'} onChange={e => { checkpoint(); setImage(e.target.value); setDirty(true); }} /></label><label>Upload blueprint<input type="file" accept="image/png,image/jpeg,image/webp" onChange={e => upload(e.target.files?.[0])} /><small>PNG, JPEG or WebP, up to 3 MB.</small></label></details>
          <label>Select a space or path point<select value={selected} onChange={e => { setSelected(e.target.value); setTool('select'); }}><option value="">Choose an item</option><optgroup label="Parking spaces">{slots.map(s => <option key={s.localId} value={s.localId}>{s.slotNumber}</option>)}</optgroup><optgroup label="Path points">{waypoints.map((w, i) => <option key={w.id} value={w.id}>Point {i + 1}</option>)}</optgroup></select></label>
          {!selectedSlot && !selectedPoint && <div className="mapping-empty"><h3>Build your floor layout</h3><p>Upload a blueprint in Floor settings, then add parking spaces on the canvas. Select an item to edit its details.</p><button className="btn btn-secondary" onClick={() => addSlot()}>Add a space</button><button className="btn btn-secondary" onClick={() => addWaypoint()}>Add a path point</button></div>}
          {selectedSlot && <><h3>Parking space</h3><label>Space label<input value={selectedSlot.slotNumber} onChange={e => changeSlot(selected, { slotNumber: e.target.value })} /></label>{slots.some(s => s.localId !== selected && s.slotNumber.trim() === selectedSlot.slotNumber.trim()) && <p className="mapping-field-error" role="alert">This label is already in use.</p>}<label>Space type<select value={selectedSlot.type} onChange={e => changeSlot(selected, { type: e.target.value })}>{types.map(t => <option key={t}>{t}</option>)}</select></label><div className="admin-toolbar"><button className="btn btn-secondary" onClick={duplicate}>Duplicate</button><button className="btn btn-secondary" onClick={removeSelected}>Remove</button></div>
            <details><summary>Position and size</summary><p className="muted">Canvas units: 1000 wide � 600 high.</p><div className="mapping-numbers">{(['canvasX', 'canvasY', 'canvasWidth', 'canvasHeight'] as const).map(key => <label key={key}>{key === 'canvasX' ? 'Position X' : key === 'canvasY' ? 'Position Y' : key.replace('canvas', '')}<input type="number" min={key === 'canvasX' || key === 'canvasY' ? 0 : 30} value={selectedSlot[key] ?? 0} onChange={e => changeSlot(selected, { [key]: Number(e.target.value) })} /></label>)}</div></details>
            <details><summary>Device assignments</summary><p className="muted">Link devices using their registered IDs. Leave blank while designing.</p><label>Sensor ID (UUID)<input value={selectedSlot.assignedSensorId || ''} onChange={e => changeSlot(selected, { assignedSensorId: e.target.value })} /></label><label>Camera ID (UUID)<input value={selectedSlot.assignedCameraId || ''} onChange={e => changeSlot(selected, { assignedCameraId: e.target.value })} /></label></details>
            <details><summary>Navigation connection</summary><label>Closest path point<select value={selectedSlot.nearestWaypointId || ''} onChange={e => changeSlot(selected, { nearestWaypointId: e.target.value })}><option value="">None</option>{waypoints.map((w, i) => <option key={w.id} value={w.id}>Point {i + 1}</option>)}</select></label></details></>}
          {selectedPoint && <><h3>Path point {waypoints.indexOf(selectedPoint) + 1}</h3><p className="muted">Connect points along entrances and aisles. Blue lines show the connections.</p><div className="mapping-numbers">{(['x', 'y'] as const).map(axis => <label key={axis}>Position {axis.toUpperCase()}<input type="number" min={0} max={axis === 'x' ? 1000 : 600} value={selectedPoint[axis]} onChange={e => { checkpoint(); setWaypoints(current => current.map(w => w.id === selected ? { ...w, [axis]: Number(e.target.value) } : w)); setDirty(true); }} /></label>)}</div><h3>Connected path points</h3>{waypoints.filter(w => w.id !== selected).map(w => <label key={w.id} className="mapping-checkbox"><input type="checkbox" checked={selectedPoint.neighbors.includes(w.id)} onChange={e => { checkpoint(); setWaypoints(current => current.map(p => p.id === selected ? { ...p, neighbors: e.target.checked ? [...p.neighbors, w.id] : p.neighbors.filter(id => id !== w.id) } : p.id === w.id ? { ...p, neighbors: e.target.checked ? [...new Set([...p.neighbors, selected])] : p.neighbors.filter(id => id !== selected) } : p)); setDirty(true); }} />Point {waypoints.indexOf(w) + 1}</label>)}{waypoints.length < 2 && <p className="muted">Add another point to connect a path.</p>}<details><summary>Advanced connection IDs</summary><p className="muted">ID: {selectedPoint.id}</p><label>Connected IDs, separated by commas<input value={selectedPoint.neighbors.join(',')} onChange={e => { checkpoint(); setWaypoints(current => current.map(w => w.id === selected ? { ...w, neighbors: e.target.value.split(',').map(id => id.trim()).filter(Boolean) } : w)); setDirty(true); }} /></label></details><button className="btn btn-secondary" onClick={removeSelected}>Remove path point</button></>}
          <details><summary>Generate a grid</summary><p className="muted">Choose the grid dimensions, preview it, then add the spaces. This does not detect spaces from the blueprint.</p><div className="mapping-numbers">{(['rows', 'cols', 'width', 'height', 'gap'] as const).map(key => <label key={key}>{key === 'cols' ? 'Spaces per row' : key === 'gap' ? 'Spacing' : key.charAt(0).toUpperCase() + key.slice(1)}<input type="number" min={key === 'gap' ? 0 : key === 'width' || key === 'height' ? 30 : 1} value={grid[key]} disabled={detections.length > 0} onChange={e => setGrid(current => ({ ...current, [key]: Number(e.target.value) }))} /></label>)}</div><label>Label prefix<input value={grid.prefix} disabled={detections.length > 0} onChange={e => setGrid(current => ({ ...current, prefix: e.target.value }))} /></label><label>Space type<select value={grid.type} disabled={detections.length > 0} onChange={e => setGrid(current => ({ ...current, type: e.target.value }))}>{types.map(t => <option key={t}>{t}</option>)}</select></label><button className="btn btn-secondary" disabled={busy} onClick={() => void detect()}>Preview generated grid</button></details>
          <details><summary>Keyboard shortcuts</summary><p className="muted">Ctrl / ? + Z: undo<br />Ctrl / ? + Shift + Z: redo<br />Ctrl / ? + D: duplicate space<br />Delete: remove selection<br />Escape: return to Select</p></details>
        </aside>
      </fieldset>}
    </div>
  </>;
}
