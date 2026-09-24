import React, { useState } from 'react';
import { MapPin, Navigation, Save, Plus } from 'lucide-react';

interface SlotNode {
  id: string;
  slotNumber: string;
  x: number;
  y: number;
  type: string;
}

interface WaypointNode {
  id: string;
  x: number;
  y: number;
}

export const FloorPlanEditor: React.FC = () => {
  const [activeTool, setActiveTool] = useState<'slot' | 'waypoint' | 'select'>('slot');
  const [slots, setSlots] = useState<SlotNode[]>([
    { id: 's-1', slotNumber: 'A-101', x: 80, y: 100, type: 'Standard' },
    { id: 's-2', slotNumber: 'A-102', x: 180, y: 100, type: 'Accessible' },
    { id: 's-3', slotNumber: 'A-103', x: 280, y: 100, type: 'EV' },
  ]);
  const [waypoints] = useState<WaypointNode[]>([
    { id: 'wp-entry', x: 40, y: 220 },
    { id: 'wp-corridor-1', x: 180, y: 220 },
    { id: 'wp-corridor-2', x: 280, y: 220 },
  ]);

  const handleCanvasClick = (e: React.MouseEvent<HTMLDivElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = Math.round(e.clientX - rect.left);
    const y = Math.round(e.clientY - rect.top);

    if (activeTool === 'slot') {
      const newSlot: SlotNode = {
        id: `s-${Date.now()}`,
        slotNumber: `A-${100 + slots.length + 1}`,
        x,
        y,
        type: 'Standard'
      };
      setSlots([...slots, newSlot]);
    }
  };

  return (
    <div className="glass-panel" style={{ padding: '24px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 600 }}>Indoor Blueprint Map Editor</h2>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.88rem' }}>
            Configure parking bays and walkable navigation waypoints for driver in-app routing.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '10px' }}>
          <button
            className={`btn ${activeTool === 'slot' ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setActiveTool('slot')}
          >
            <Plus size={16} /> Add Slot
          </button>
          <button
            className={`btn ${activeTool === 'waypoint' ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setActiveTool('waypoint')}
          >
            <Navigation size={16} /> Waypoint
          </button>
          <button className="btn btn-secondary">
            <Save size={16} /> Save Floor Plan
          </button>
        </div>
      </div>

      {/* Blueprint Canvas Area */}
      <div
        onClick={handleCanvasClick}
        style={{
          width: '100%',
          height: '450px',
          backgroundColor: '#0a0e17',
          backgroundImage: 'radial-gradient(rgba(255, 255, 255, 0.1) 1px, transparent 1px)',
          backgroundSize: '24px 24px',
          borderRadius: 'var(--radius-md)',
          position: 'relative',
          border: '1px dashed var(--border-color)',
          cursor: activeTool === 'slot' ? 'crosshair' : 'default',
          overflow: 'hidden'
        }}
      >
        {/* Render Waypoints */}
        {waypoints.map((wp: WaypointNode) => (
          <div
            key={wp.id}
            style={{
              position: 'absolute',
              left: `${wp.x}px`,
              top: `${wp.y}px`,
              width: '16px',
              height: '16px',
              borderRadius: '50%',
              backgroundColor: '#3b82f6',
              boxShadow: '0 0 10px #3b82f6',
              transform: 'translate(-50%, -50%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'white',
              fontSize: '10px'
            }}
            title={`Waypoint: ${wp.id}`}
          />
        ))}

        {/* Render Slots */}
        {slots.map((slot: SlotNode) => (
          <div
            key={slot.id}
            style={{
              position: 'absolute',
              left: `${slot.x}px`,
              top: `${slot.y}px`,
              width: '80px',
              height: '50px',
              border: '2px solid #6366f1',
              backgroundColor: 'rgba(99, 102, 241, 0.15)',
              borderRadius: '6px',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#f9fafb',
              fontSize: '11px',
              fontWeight: 600,
              boxShadow: '0 2px 8px rgba(99, 102, 241, 0.2)'
            }}
          >
            <MapPin size={12} color="#818cf8" />
            <span>{slot.slotNumber}</span>
            <span style={{ fontSize: '9px', color: '#9ca3af' }}>{slot.type}</span>
          </div>
        ))}
      </div>
    </div>
  );
};
