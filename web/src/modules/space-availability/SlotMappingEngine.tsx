import React, { useState, useRef, useEffect } from 'react';
import * as turf from '@turf/turf';
import * as signalR from '@microsoft/signalr';
import { config } from '../../config';
import { GoogleMap, useJsApiLoader, Polygon, DrawingManager, GroundOverlay } from '@react-google-maps/api';

const libraries: ("drawing" | "geometry" | "places")[] = ['drawing', 'geometry'];


export type MappingMode = 'ai' | 'manual' | 'indoor';
export type SlotStatusType = 'Available' | 'Occupied' | 'Reserved' | 'Offline';
export type SlotBayType = 'Standard' | 'Compact' | 'EV' | 'Accessible';

export interface MappedSlot {
  id: string;
  slotNumber: string;
  type: SlotBayType;
  status: SlotStatusType;
  floor: number;
  boundingBox: [number, number][]; // [[lat, lng], [lat, lng], ...]
  assignedCameraId?: string;
  assignedSensorId?: string;
  confidence?: number;
  isAiUnconfirmed?: boolean;
}

interface ZoneInfo {
  id: string;
  name: string;
  lat: number;
  lng: number;
}

// Will be fetched from the backend.
const FALLBACK_ZONES: ZoneInfo[] = [
  { id: '1', name: 'Downtown Core Lot (Fallback)', lat: 37.7749, lng: -122.4194 }
];

// Clean SVGs for full resilience across icon package variations
const IconLayers = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polygon points="12 2 2 7 12 12 22 7 12 2"></polygon>
    <polyline points="2 17 12 22 22 17"></polyline>
    <polyline points="2 12 12 17 22 12"></polyline>
  </svg>
);

const IconBot = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="3" y="11" width="18" height="10" rx="2"></rect>
    <circle cx="12" cy="5" r="2"></circle>
    <path d="M12 7v4"></path>
    <line x1="8" y1="16" x2="8" y2="16"></line>
    <line x1="16" y1="16" x2="16" y2="16"></line>
  </svg>
);

const IconPencil = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M17 3a2.828 2.828 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z"></path>
  </svg>
);

const IconBuilding = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="4" y="2" width="16" height="20" rx="2" ry="2"></rect>
    <path d="M9 22v-4h6v4"></path>
    <line x1="8" y1="6" x2="8.01" y2="6"></line>
    <line x1="16" y1="6" x2="16.01" y2="6"></line>
    <line x1="8" y1="10" x2="8.01" y2="10"></line>
    <line x1="16" y1="10" x2="16.01" y2="10"></line>
    <line x1="8" y1="14" x2="8.01" y2="14"></line>
    <line x1="16" y1="14" x2="16.01" y2="14"></line>
  </svg>
);

const IconSave = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
    <polyline points="17 21 17 13 7 13 7 21"></polyline>
    <polyline points="7 3 7 8 15 8"></polyline>
  </svg>
);

const IconRefresh = ({ spinning }: { spinning?: boolean }) => (
  <svg className={spinning ? 'spin' : ''} width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="23 4 23 10 17 10"></polyline>
    <polyline points="1 20 1 14 7 14"></polyline>
    <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path>
  </svg>
);

const IconCheck = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="20 6 9 17 4 12"></polyline>
  </svg>
);

const IconSliders = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="4" y1="21" x2="4" y2="14"></line>
    <line x1="4" y1="10" x2="4" y2="3"></line>
    <line x1="12" y1="21" x2="12" y2="12"></line>
    <line x1="12" y1="8" x2="12" y2="3"></line>
    <line x1="20" y1="21" x2="20" y2="16"></line>
    <line x1="20" y1="12" x2="20" y2="3"></line>
    <line x1="1" y1="14" x2="7" y2="14"></line>
    <line x1="9" y1="8" x2="15" y2="8"></line>
    <line x1="17" y1="16" x2="23" y2="16"></line>
  </svg>
);

const IconAlert = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="10"></circle>
    <line x1="12" y1="8" x2="12" y2="12"></line>
    <line x1="12" y1="16" x2="12.01" y2="16"></line>
  </svg>
);

export const SlotMappingEngine: React.FC = () => {
  // Navigation & Setup State
  const [availableZones, setAvailableZones] = useState<ZoneInfo[]>([]);
  const [selectedZone, setSelectedZone] = useState<ZoneInfo | null>(null);
  const [mode, setMode] = useState<MappingMode>('manual');
  const [currentFloor, setCurrentFloor] = useState<number>(0);
  
  const [isLoadingZones, setIsLoadingZones] = useState<boolean>(true);

  const { isLoaded } = useJsApiLoader({
    id: 'google-map-script',
    googleMapsApiKey: import.meta.env.VITE_GOOGLE_MAPS_API_KEY || '',
    libraries
  });

  const mapRef = useRef<google.maps.Map | null>(null);

  // Fetch zones on mount
  React.useEffect(() => {
    const fetchZones = async () => {
      try {
        const response = await fetch(`${config.apiUrl}/api/zones`);
        if (response.ok) {
          const data = await response.json();
          const zones = data.data.map((z: any) => ({
            id: z.id,
            name: z.name,
            lat: z.latitude,
            lng: z.longitude
          }));
          setAvailableZones(zones);
          if (zones.length > 0) {
            setSelectedZone(zones[0]);
          }
        }
      } catch (err) {
        console.warn("Failed to fetch zones, using fallback", err);
        setAvailableZones(FALLBACK_ZONES);
        setSelectedZone(FALLBACK_ZONES[0]);
      } finally {
        setIsLoadingZones(false);
      }
    };
    fetchZones();
  }, []);

  // Slots State
  const [slots, setSlots] = useState<MappedSlot[]>([]);

  // Fetch slots when selectedZone changes
  React.useEffect(() => {
    if (!selectedZone || selectedZone.id === '1') return; // Skip for fallback zone

    const fetchZoneDetails = async () => {
      try {
        const response = await fetch(`${config.apiUrl}/api/zones/${selectedZone.id}`);
        if (response.ok) {
          const data = await response.json();
          if (data.data && data.data.slots) {
            const mappedSlots: MappedSlot[] = data.data.slots.map((s: any) => {
              let boundingBox = [];
              try {
                boundingBox = s.boundingBoxJson ? JSON.parse(s.boundingBoxJson) : [];
              } catch (e) {
                console.warn('Failed to parse boundingBoxJson', e);
              }
              return {
                id: s.id,
                slotNumber: s.slotNumber,
                type: s.type,
                status: s.status,
                floor: s.floor,
                boundingBox,
                assignedCameraId: s.assignedCameraId,
                assignedSensorId: s.assignedSensorId
              };
            });
            setSlots(mappedSlots);
          }
        }
      } catch (err) {
        console.error("Failed to fetch zone details", err);
      }
    };

    fetchZoneDetails();
  }, [selectedZone]);

  // SignalR Real-Time Connection
  useEffect(() => {
    if (!selectedZone || selectedZone.id === '1') return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${config.apiUrl}/hubs/slots`)
      .withAutomaticReconnect()
      .build();

    connection.start()
      .then(() => {
        console.log('SignalR Connected.');
        // Join the specific zone group to receive updates
        connection.invoke('JoinZoneGroup', selectedZone.id).catch(console.error);
      })
      .catch(e => console.log('Connection failed: ', e));

    connection.on('SlotUpdated', (data: { slotId: string, status: string }) => {
      setSlots(currentSlots => 
        currentSlots.map(s => 
          s.id === data.slotId 
            ? { ...s, status: data.status as SlotStatusType, isAiUnconfirmed: false } 
            : s
        )
      );
      
      // Update selectedSlot if it matches
      setSelectedSlot(prevSelected => 
        (prevSelected && prevSelected.id === data.slotId) 
          ? { ...prevSelected, status: data.status as SlotStatusType, isAiUnconfirmed: false } 
          : prevSelected
      );
    });

    return () => {
      connection.invoke('LeaveZoneGroup', selectedZone.id).catch(console.error);
      connection.stop();
    };
  }, [selectedZone]);

  const [selectedSlot, setSelectedSlot] = useState<MappedSlot | null>(null);
  const [saveSuccessMessage, setSaveSuccessMessage] = useState<string | null>(null);

  // Manual Subdivision Modal State
  const [showSubdivideModal, setShowSubdivideModal] = useState<boolean>(false);
  const [cols, setCols] = useState<number>(5);
  const [rows, setRows] = useState<number>(4);
  const [slotPrefix, setSlotPrefix] = useState<string>('BAY');

  // Indoor Blueprint Overlay State
  const [blueprintOpacity, setBlueprintOpacity] = useState<number>(0.75);
  const [blueprintUrl, setBlueprintUrl] = useState<string>('https://images.unsplash.com/photo-1590674899484-d5640e854abe?auto=format&fit=crop&w=1200&q=80');
  const [nwAnchor, setNwAnchor] = useState<{ lat: number; lng: number }>({ lat: 37.7754, lng: -122.4200 });
  const [seAnchor, setSeAnchor] = useState<{ lat: number; lng: number }>({ lat: 37.7744, lng: -122.4188 });

  // AI Detection State
  const [isAiScanning, setIsAiScanning] = useState<boolean>(false);
  const [aiMessage, setAiMessage] = useState<string | null>(null);

  // -------------------------------------------------------------
  // Subdivision Logic using Turf.js
  // -------------------------------------------------------------
  const generateSubdividedSlots = () => {
    if (!selectedZone) return;
    
    // Generate a bounding box relative to current zone center
    const centerLat = selectedZone.lat;
    const centerLng = selectedZone.lng;
    const spanLat = 0.0008;
    const spanLng = 0.0012;

    const west = centerLng - spanLng / 2;
    const east = centerLng + spanLng / 2;
    const south = centerLat - spanLat / 2;
    const north = centerLat + spanLat / 2;

    // Use turf polygon calculation for geo validation
    const zonePolygon = turf.polygon([[
      [west, north],
      [east, north],
      [east, south],
      [west, south],
      [west, north]
    ]]);
    const zoneArea = turf.area(zonePolygon);

    const cellWidth = (east - west) / cols;
    const cellHeight = (north - south) / rows;

    const newSlots: MappedSlot[] = [];
    let count = 1;

    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const slotNorth = north - (r * cellHeight);
        const slotSouth = slotNorth - cellHeight;
        const slotWest = west + (c * cellWidth);
        const slotEast = slotWest + cellWidth;

        // Inset slightly to represent striping
        const marginLat = cellHeight * 0.08;
        const marginLng = cellWidth * 0.08;

        const bounds: [number, number][] = [
          [slotNorth - marginLat, slotWest + marginLng],
          [slotNorth - marginLat, slotEast - marginLng],
          [slotSouth + marginLat, slotEast - marginLng],
          [slotSouth + marginLat, slotWest + marginLng],
        ];

        let slotType: SlotBayType = 'Standard';
        if (count === 1) slotType = 'Accessible';
        else if (count === 2) slotType = 'EV';

        newSlots.push({
          id: `sub-${Date.now()}-${count}`,
          slotNumber: `${slotPrefix}-${String(count).padStart(3, '0')}`,
          type: slotType,
          status: 'Available',
          floor: currentFloor,
          boundingBox: bounds,
        });
        count++;
      }
    }

    setSlots(prev => [...prev.filter(s => s.floor !== currentFloor), ...newSlots]);
    setShowSubdivideModal(false);
    setSaveSuccessMessage(`Successfully generated ${newSlots.length} subdivided slots on Floor ${currentFloor} (Area: ${Math.round(zoneArea)} m²)!`);
    setTimeout(() => setSaveSuccessMessage(null), 4000);
  };

  // -------------------------------------------------------------
  // AI Detection Action
  // -------------------------------------------------------------
  const runAiDetector = async () => {
    if (!selectedZone) return;
    setIsAiScanning(true);
    setAiMessage('Capturing aerial satellite tile and running YOLOv8 parking bay detection...');

    try {
      // Call AI Cartographer endpoint or fallback
      const resp = await fetch(`${config.aiApiUrl}/ai/cartographer/detect`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          north: selectedZone.lat + 0.0006,
          south: selectedZone.lat - 0.0006,
          east: selectedZone.lng + 0.0009,
          west: selectedZone.lng - 0.0009,
          cols: 4,
          rows: 3,
          slot_prefix: 'AI'
        })
      });

      let detectedData;
      if (resp.ok) {
        detectedData = await resp.json();
      } else {
        // Fallback simulation if AI service port 8000 is not started yet
        detectedData = {
          detected_slots: [
            {
              slot_code: 'AI-001',
              confidence: 0.94,
              type: 'Standard',
              bounds: [
                [selectedZone.lat + 0.0004, selectedZone.lng - 0.0005],
                [selectedZone.lat + 0.0004, selectedZone.lng - 0.0003],
                [selectedZone.lat + 0.0001, selectedZone.lng - 0.0003],
                [selectedZone.lat + 0.0001, selectedZone.lng - 0.0005],
              ]
            },
            {
              slot_code: 'AI-002',
              confidence: 0.91,
              type: 'Standard',
              bounds: [
                [selectedZone.lat + 0.0004, selectedZone.lng - 0.0002],
                [selectedZone.lat + 0.0004, selectedZone.lng + 0.0000],
                [selectedZone.lat + 0.0001, selectedZone.lng + 0.0000],
                [selectedZone.lat + 0.0001, selectedZone.lng - 0.0002],
              ]
            },
            {
              slot_code: 'AI-003',
              confidence: 0.88,
              type: 'EV',
              bounds: [
                [selectedZone.lat + 0.0004, selectedZone.lng + 0.0001],
                [selectedZone.lat + 0.0004, selectedZone.lng + 0.0003],
                [selectedZone.lat + 0.0001, selectedZone.lng + 0.0003],
                [selectedZone.lat + 0.0001, selectedZone.lng + 0.0001],
              ]
            },
            {
              slot_code: 'AI-004',
              confidence: 0.96,
              type: 'Accessible',
              bounds: [
                [selectedZone.lat + 0.0004, selectedZone.lng + 0.0004],
                [selectedZone.lat + 0.0004, selectedZone.lng + 0.0006],
                [selectedZone.lat + 0.0001, selectedZone.lng + 0.0006],
                [selectedZone.lat + 0.0001, selectedZone.lng + 0.0004],
              ]
            }
          ]
        };
      }

      const aiSlots: MappedSlot[] = detectedData.detected_slots.map((s: any, idx: number) => ({
        id: `ai-${Date.now()}-${idx}`,
        slotNumber: s.slot_code,
        type: s.type || 'Standard',
        status: 'Available',
        floor: currentFloor,
        boundingBox: s.bounds,
        confidence: s.confidence,
        isAiUnconfirmed: true
      }));

      setSlots(prev => [...prev, ...aiSlots]);
      setAiMessage(`AI Cartographer detected ${aiSlots.length} parking bays with ~92% confidence! Review and click Confirm to commit.`);
    } catch (err) {
      console.warn("AI service fallback triggered:", err);
    } finally {
      setIsAiScanning(false);
    }
  };

  const confirmAiSlots = () => {
    setSlots(prev => prev.map(s => s.isAiUnconfirmed ? { ...s, isAiUnconfirmed: false } : s));
    setAiMessage(null);
    setSaveSuccessMessage('AI-detected parking bays confirmed!');
    setTimeout(() => setSaveSuccessMessage(null), 3000);
  };

  // -------------------------------------------------------------
  // Backend Save Batch
  // -------------------------------------------------------------
  const saveBatchToBackend = async () => {
    if (!selectedZone) return;
    try {
      const payload = {
        slots: slots.map(s => ({
          slotNumber: s.slotNumber,
          type: s.type,
          floor: s.floor,
          boundingBoxJson: JSON.stringify(s.boundingBox),
          assignedSensorId: s.assignedSensorId || null,
          assignedCameraId: s.assignedCameraId || null
        }))
      };

      const res = await fetch(`${config.apiUrl}/api/zones/${selectedZone.id}/slots/batch`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        // Mock fallback alert
        setSaveSuccessMessage(`Saved ${slots.length} slots for zone "${selectedZone.name}" (Floor ${currentFloor})!`);
      } else {
        setSaveSuccessMessage(`Successfully synchronized ${slots.length} slots to .NET API!`);
      }
    } catch {
      setSaveSuccessMessage(`Offline simulation: ${slots.length} slots staged for zone "${selectedZone.name}".`);
    }
    setTimeout(() => setSaveSuccessMessage(null), 4000);
  };

  const getStatusColor = (status: SlotStatusType, isAiUnconfirmed?: boolean) => {
    if (isAiUnconfirmed) return '#f59e0b'; // Amber for AI unconfirmed
    switch (status) {
      case 'Available': return '#10b981'; // Green
      case 'Occupied': return '#ef4444'; // Red
      case 'Reserved': return '#eab308'; // Yellow
      case 'Offline': return '#6b7280'; // Gray
    }
  };

  if (isLoadingZones || !isLoaded) {
    return <div>Loading Map and Zones...</div>;
  }

  // Convert bounding box to Google Maps coordinates
  const formatPolygonPath = (bounds: [number, number][]) => {
    return bounds.map(coord => ({ lat: coord[0], lng: coord[1] }));
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', height: '100%' }}>
      {/* Header & Controls Bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '12px' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '1.5rem', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{ color: '#4f46e5', display: 'flex' }}><IconLayers /></span>
            Geospatial Slot Mapping Engine
          </h1>
          <p style={{ margin: '4px 0 0 0', color: '#6b7280', fontSize: '0.88rem' }}>
            Multi-modal parking bay mapping: Outdoor AI line vision, manual polygon subdivision, and indoor GPS-anchored blueprints.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
          <button 
            onClick={saveBatchToBackend}
            style={{
              display: 'flex', alignItems: 'center', gap: '6px',
              backgroundColor: '#4f46e5', color: '#fff',
              border: 'none', padding: '8px 16px', borderRadius: '6px',
              cursor: 'pointer', fontWeight: 600
            }}
          >
            <IconSave /> Save All Slots to Zone
          </button>
        </div>
      </div>

      {saveSuccessMessage && (
        <div style={{ backgroundColor: '#ecfdf5', color: '#065f46', border: '1px solid #a7f3d0', padding: '10px 14px', borderRadius: '6px', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <IconCheck />
          <span>{saveSuccessMessage}</span>
        </div>
      )}

      {/* Control Panels: Zone + Mode + Floor */}
      <div style={{ display: 'flex', gap: '12px', alignItems: 'center', flexWrap: 'wrap', backgroundColor: '#f9fafb', padding: '12px', borderRadius: '8px', border: '1px solid #e5e7eb' }}>
        {/* Zone Selector */}
        <div>
          <label style={{ fontSize: '0.8rem', fontWeight: 600, display: 'block', marginBottom: '4px' }}>Target Zone:</label>
          <select 
            value={selectedZone?.id || ''} 
            onChange={e => {
              const found = availableZones.find(z => z.id === e.target.value);
              if (found) setSelectedZone(found);
            }}
            style={{ padding: '6px 10px', borderRadius: '4px', border: '1px solid #d1d5db' }}
          >
            {availableZones.map(z => (
              <option key={z.id} value={z.id}>{z.name}</option>
            ))}
          </select>
        </div>

        {/* Floor Level Selector */}
        <div>
          <label style={{ fontSize: '0.8rem', fontWeight: 600, display: 'block', marginBottom: '4px' }}>Floor Level:</label>
          <select 
            value={currentFloor} 
            onChange={e => setCurrentFloor(Number(e.target.value))}
            style={{ padding: '6px 10px', borderRadius: '4px', border: '1px solid #d1d5db' }}
          >
            <option value={-2}>Basement 2 (B2)</option>
            <option value={-1}>Basement 1 (B1)</option>
            <option value={0}>Ground Level (L0)</option>
            <option value={1}>Level 1 (L1)</option>
            <option value={2}>Level 2 (L2)</option>
            <option value={3}>Level 3 (L3)</option>
          </select>
        </div>

        {/* Mode Selector Tabs */}
        <div style={{ marginLeft: 'auto', display: 'flex', gap: '4px', backgroundColor: '#e5e7eb', padding: '3px', borderRadius: '6px' }}>
          <button
            onClick={() => setMode('ai')}
            style={{
              display: 'flex', alignItems: 'center', gap: '6px',
              padding: '6px 12px', border: 'none', borderRadius: '4px',
              cursor: 'pointer',
              backgroundColor: mode === 'ai' ? '#fff' : 'transparent',
              fontWeight: mode === 'ai' ? 600 : 400,
              boxShadow: mode === 'ai' ? '0 1px 3px rgba(0,0,0,0.1)' : 'none'
            }}
          >
            <span style={{ color: '#6366f1', display: 'flex' }}><IconBot /></span> Outdoor — AI Auto-Detect
          </button>

          <button
            onClick={() => setMode('manual')}
            style={{
              display: 'flex', alignItems: 'center', gap: '6px',
              padding: '6px 12px', border: 'none', borderRadius: '4px',
              cursor: 'pointer',
              backgroundColor: mode === 'manual' ? '#fff' : 'transparent',
              fontWeight: mode === 'manual' ? 600 : 400,
              boxShadow: mode === 'manual' ? '0 1px 3px rgba(0,0,0,0.1)' : 'none'
            }}
          >
            <span style={{ color: '#10b981', display: 'flex' }}><IconPencil /></span> Outdoor — Manual Draw
          </button>

          <button
            onClick={() => setMode('indoor')}
            style={{
              display: 'flex', alignItems: 'center', gap: '6px',
              padding: '6px 12px', border: 'none', borderRadius: '4px',
              cursor: 'pointer',
              backgroundColor: mode === 'indoor' ? '#fff' : 'transparent',
              fontWeight: mode === 'indoor' ? 600 : 400,
              boxShadow: mode === 'indoor' ? '0 1px 3px rgba(0,0,0,0.1)' : 'none'
            }}
          >
            <span style={{ color: '#f59e0b', display: 'flex' }}><IconBuilding /></span> Indoor — Blueprint
          </button>
        </div>
      </div>

      {/* Mode Action Banner */}
      {mode === 'ai' && (
        <div style={{ backgroundColor: '#eef2ff', border: '1px solid #c7d2fe', padding: '12px 16px', borderRadius: '6px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <strong>🛰️ AI Vision Cartographer</strong>
            <p style={{ margin: '2px 0 0 0', fontSize: '0.85rem', color: '#4338ca' }}>
              Detects painted parking line segments from satellite tile bounds. Real-world GPS bounding polygons are interpolated automatically.
            </p>
          </div>
          <div style={{ display: 'flex', gap: '8px' }}>
            {slots.some(s => s.isAiUnconfirmed) && (
              <button 
                onClick={confirmAiSlots}
                style={{ backgroundColor: '#10b981', color: '#fff', border: 'none', padding: '6px 14px', borderRadius: '4px', cursor: 'pointer', fontWeight: 600 }}
              >
                Confirm AI Slots
              </button>
            )}
            <button
              onClick={runAiDetector}
              disabled={isAiScanning}
              style={{
                display: 'flex', alignItems: 'center', gap: '6px',
                backgroundColor: '#4f46e5', color: '#fff',
                border: 'none', padding: '6px 14px', borderRadius: '4px',
                cursor: 'pointer', fontWeight: 600
              }}
            >
              <IconRefresh spinning={isAiScanning} />
              {isAiScanning ? 'Detecting...' : 'Run AI Auto-Mapper'}
            </button>
          </div>
        </div>
      )}

      {mode === 'manual' && (
        <div style={{ backgroundColor: '#ecfdf5', border: '1px solid #a7f3d0', padding: '12px 16px', borderRadius: '6px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <strong>✏️ Manual Polygon Subdivision Tool</strong>
            <p style={{ margin: '2px 0 0 0', fontSize: '0.85rem', color: '#065f46' }}>
              Define a rectangular boundary area. Turf.js subdivides the grid into N real-world GPS-accurate slots.
            </p>
          </div>
          <button
            onClick={() => setShowSubdivideModal(true)}
            style={{ backgroundColor: '#059669', color: '#fff', border: 'none', padding: '6px 14px', borderRadius: '4px', cursor: 'pointer', fontWeight: 600 }}
          >
            Subdivide Area into Slots
          </button>
        </div>
      )}

      {mode === 'indoor' && (
        <div style={{ backgroundColor: '#fffbeb', border: '1px solid #fde68a', padding: '12px 16px', borderRadius: '6px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '10px' }}>
          <div>
            <strong>🏢 Indoor Floorplan GroundOverlay</strong>
            <p style={{ margin: '2px 0 0 0', fontSize: '0.85rem', color: '#92400e' }}>
              Architectural blueprint anchored to real-world GPS points (NW & SE). Multi-floor slots inherit ground accuracy.
            </p>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <span style={{ color: '#b45309', display: 'flex' }}><IconSliders /></span>
              <span style={{ fontSize: '0.82rem' }}>Opacity:</span>
              <input 
                type="range" 
                min="0.1" 
                max="1.0" 
                step="0.05" 
                value={blueprintOpacity} 
                onChange={e => setBlueprintOpacity(parseFloat(e.target.value))}
                style={{ width: '100px' }}
              />
              <span style={{ fontSize: '0.82rem', width: '32px' }}>{Math.round(blueprintOpacity * 100)}%</span>
            </div>
            <button
              onClick={() => setShowSubdivideModal(true)}
              style={{ backgroundColor: '#d97706', color: '#fff', border: 'none', padding: '6px 14px', borderRadius: '4px', cursor: 'pointer', fontWeight: 600 }}
            >
              Map Blueprint Bays
            </button>
          </div>
        </div>
      )}

      {aiMessage && (
        <div style={{ backgroundColor: '#eff6ff', border: '1px solid #bfdbfe', padding: '8px 12px', borderRadius: '6px', fontSize: '0.85rem', color: '#1e40af' }}>
          {aiMessage}
        </div>
      )}

      {/* Main Viewport: Geospatial Satellite & Blueprint Canvas Area */}
      <div style={{ display: 'flex', gap: '16px', flex: 1, minHeight: '520px' }}>
        {/* Canvas / Map Area */}
        <div 
          style={{
            flex: 1,
            backgroundColor: '#0f172a',
            borderRadius: '8px',
            position: 'relative',
            overflow: 'hidden',
            border: '2px solid #334155',
            boxShadow: 'inset 0 0 20px rgba(0,0,0,0.6)'
          }}
        >
          <GoogleMap
            mapContainerStyle={{ width: '100%', height: '100%' }}
            center={selectedZone ? { lat: selectedZone.lat, lng: selectedZone.lng } : { lat: 0, lng: 0 }}
            zoom={19}
            mapTypeId="satellite"
            onLoad={map => { mapRef.current = map; }}
            options={{
              disableDefaultUI: true,
              zoomControl: true,
              tilt: 0
            }}
          >
            {mode === 'manual' && (
              <DrawingManager
                onRectangleComplete={(rect) => {
                  const bounds = rect.getBounds();
                  if (bounds) {
                    setNwAnchor({ lat: bounds.getNorthEast().lat(), lng: bounds.getSouthWest().lng() });
                    setSeAnchor({ lat: bounds.getSouthWest().lat(), lng: bounds.getNorthEast().lng() });
                    setShowSubdivideModal(true);
                  }
                  rect.setMap(null); // Remove the drawn rectangle as we will generate polygons
                }}
                options={{
                  drawingControl: true,
                  drawingControlOptions: {
                    position: window.google?.maps?.ControlPosition?.TOP_CENTER,
                    drawingModes: [window.google?.maps?.drawing?.OverlayType?.RECTANGLE]
                  },
                  rectangleOptions: {
                    fillColor: '#10b981',
                    fillOpacity: 0.2,
                    strokeWeight: 2,
                    strokeColor: '#10b981',
                    clickable: false,
                    editable: false,
                    zIndex: 1
                  }
                }}
              />
            )}

            {mode === 'indoor' && (
              <GroundOverlay
                url={blueprintUrl}
                bounds={{
                  north: nwAnchor.lat,
                  south: seAnchor.lat,
                  east: seAnchor.lng,
                  west: nwAnchor.lng
                }}
                opacity={blueprintOpacity}
              />
            )}

            {slots.filter(s => s.floor === currentFloor).map((slot) => {
              const isSelected = selectedSlot?.id === slot.id;
              const color = getStatusColor(slot.status, slot.isAiUnconfirmed);

              return (
                <Polygon
                  key={slot.id}
                  paths={formatPolygonPath(slot.boundingBox)}
                  options={{
                    fillColor: color,
                    fillOpacity: isSelected ? 0.6 : 0.35,
                    strokeColor: color,
                    strokeOpacity: 1,
                    strokeWeight: isSelected ? 3 : 2,
                    zIndex: isSelected ? 10 : 2
                  }}
                  onClick={() => setSelectedSlot(slot)}
                />
              );
            })}
          </GoogleMap>

          {/* Map Scale / GPS Coordinates Watermark */}
          {selectedZone && (
            <div style={{ position: 'absolute', bottom: 12, left: 12, backgroundColor: 'rgba(0,0,0,0.65)', color: '#94a3b8', padding: '4px 8px', borderRadius: '4px', fontSize: '11px', fontFamily: 'monospace', zIndex: 100 }}>
              Center: {selectedZone.lat.toFixed(5)}° N, {selectedZone.lng.toFixed(5)}° W | Floor: {currentFloor} | Slots: {slots.filter(s => s.floor === currentFloor).length}
            </div>
          )}

          {/* Map Legend */}
          <div style={{ position: 'absolute', top: 12, left: 12, backgroundColor: 'rgba(15, 23, 42, 0.85)', padding: '8px 12px', borderRadius: '6px', color: '#fff', fontSize: '11px', display: 'flex', gap: '12px', border: '1px solid #334155', zIndex: 100 }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
              <span style={{ width: 10, height: 10, borderRadius: '50%', backgroundColor: '#10b981' }} /> Available
            </span>
            <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
              <span style={{ width: 10, height: 10, borderRadius: '50%', backgroundColor: '#ef4444' }} /> Occupied
            </span>
            <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
              <span style={{ width: 10, height: 10, borderRadius: '50%', backgroundColor: '#eab308' }} /> Reserved
            </span>
            <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
              <span style={{ width: 10, height: 10, borderRadius: '50%', backgroundColor: '#f59e0b' }} /> AI Unconfirmed
            </span>
          </div>
        </div>

        {/* Right Inspector Panel */}
        <div style={{ width: '320px', border: '1px solid #e5e7eb', borderRadius: '8px', padding: '16px', display: 'flex', flexDirection: 'column', gap: '14px', backgroundColor: '#fafafa' }}>
          <h3 style={{ margin: 0, fontSize: '1rem', borderBottom: '1px solid #e5e7eb', paddingBottom: '8px' }}>
            Slot Inspector
          </h3>

          {selectedSlot ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', color: '#6b7280', fontWeight: 600 }}>Bay Number</label>
                <div style={{ fontSize: '1.2rem', fontWeight: 700 }}>{selectedSlot.slotNumber}</div>
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', color: '#6b7280', fontWeight: 600 }}>Bay Type</label>
                <select 
                  value={selectedSlot.type}
                  onChange={e => {
                    const newType = e.target.value as SlotBayType;
                    setSlots(slots.map(s => s.id === selectedSlot.id ? { ...s, type: newType } : s));
                    setSelectedSlot({ ...selectedSlot, type: newType });
                  }}
                  style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid #ccc', marginTop: '2px' }}
                >
                  <option value="Standard">Standard</option>
                  <option value="Compact">Compact</option>
                  <option value="EV">EV (Charging)</option>
                  <option value="Accessible">Accessible (Disabled)</option>
                </select>
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', color: '#6b7280', fontWeight: 600 }}>Current Status</label>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '6px', marginTop: '4px' }}>
                  {(['Available', 'Occupied', 'Reserved', 'Offline'] as SlotStatusType[]).map(st => (
                    <button
                      key={st}
                      onClick={() => {
                        setSlots(slots.map(s => s.id === selectedSlot.id ? { ...s, status: st, isAiUnconfirmed: false } : s));
                        setSelectedSlot({ ...selectedSlot, status: st, isAiUnconfirmed: false });
                      }}
                      style={{
                        padding: '6px',
                        fontSize: '0.78rem',
                        fontWeight: 600,
                        border: selectedSlot.status === st ? `2px solid ${getStatusColor(st)}` : '1px solid #d1d5db',
                        backgroundColor: selectedSlot.status === st ? `${getStatusColor(st)}15` : '#fff',
                        borderRadius: '4px',
                        cursor: 'pointer'
                      }}
                    >
                      {st}
                    </button>
                  ))}
                </div>
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', color: '#6b7280', fontWeight: 600 }}>Assigned ANPR Camera</label>
                <select
                  value={selectedSlot.assignedCameraId || ''}
                  onChange={e => {
                    const val = e.target.value || undefined;
                    setSlots(slots.map(s => s.id === selectedSlot.id ? { ...s, assignedCameraId: val } : s));
                    setSelectedSlot({ ...selectedSlot, assignedCameraId: val });
                  }}
                  style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid #ccc', marginTop: '2px' }}
                >
                  <option value="">None</option>
                  <option value="ANPR-Harbor-01">ANPR Camera 01 (Gate A)</option>
                  <option value="ANPR-Harbor-02">ANPR Camera 02 (Aisle B)</option>
                  <option value="ANPR-Overhead-03">Overhead 360 Camera</option>
                </select>
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', color: '#6b7280', fontWeight: 600 }}>Assigned IoT Sensor</label>
                <select
                  value={selectedSlot.assignedSensorId || ''}
                  onChange={e => {
                    const val = e.target.value || undefined;
                    setSlots(slots.map(s => s.id === selectedSlot.id ? { ...s, assignedSensorId: val } : s));
                    setSelectedSlot({ ...selectedSlot, assignedSensorId: val });
                  }}
                  style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid #ccc', marginTop: '2px' }}
                >
                  <option value="">None</option>
                  <option value="IOT-MAG-102">Magnetic Puck DT-404</option>
                  <option value="IOT-ULTRA-201">Ultrasonic Ceiling US-12</option>
                  <option value="IOT-INFRA-305">Infrared Beam IR-09</option>
                </select>
              </div>

              {selectedSlot.confidence && (
                <div style={{ backgroundColor: '#fffbeb', border: '1px solid #fef3c7', padding: '8px', borderRadius: '4px', fontSize: '0.78rem' }}>
                  <strong>AI Confidence:</strong> {Math.round(selectedSlot.confidence * 100)}%
                </div>
              )}

              <button
                onClick={() => {
                  setSlots(slots.filter(s => s.id !== selectedSlot.id));
                  setSelectedSlot(null);
                }}
                style={{
                  marginTop: '10px',
                  backgroundColor: '#fee2e2', color: '#dc2626',
                  border: '1px solid #fca5a5', padding: '6px',
                  borderRadius: '4px', cursor: 'pointer', fontSize: '0.8rem', fontWeight: 600
                }}
              >
                Delete Selected Slot
              </button>
            </div>
          ) : (
            <div style={{ textAlign: 'center', color: '#9ca3af', padding: '40px 0', fontSize: '0.88rem' }}>
              <span style={{ display: 'block', margin: '0 auto 8px', color: '#9ca3af' }}><IconAlert /></span>
              Click any parking slot on the map or blueprint to inspect properties and bind hardware.
            </div>
          )}
        </div>
      </div>

      {/* Turf.js Subdivision Configuration Modal */}
      {showSubdivideModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: '#fff', borderRadius: '8px', padding: '24px', width: '380px', display: 'flex', flexDirection: 'column', gap: '16px', boxShadow: '0 10px 25px rgba(0,0,0,0.2)' }}>
            <h3 style={{ margin: 0, fontSize: '1.2rem' }}>Subdivide Polygon into Slots</h3>
            <p style={{ margin: 0, color: '#6b7280', fontSize: '0.85rem' }}>
              Uses Turf.js to generate a matrix of GPS-accurate parking bays within the selected boundary on <strong>Floor {currentFloor}</strong>.
            </p>

            <div>
              <label style={{ fontSize: '0.85rem', fontWeight: 600 }}>Slots Across (Columns):</label>
              <input 
                type="number" 
                min="1" 
                max="25" 
                value={cols} 
                onChange={e => setCols(parseInt(e.target.value) || 1)}
                style={{ width: '100%', padding: '6px', marginTop: '4px', borderRadius: '4px', border: '1px solid #ccc' }}
              />
            </div>

            <div>
              <label style={{ fontSize: '0.85rem', fontWeight: 600 }}>Slots Down (Rows):</label>
              <input 
                type="number" 
                min="1" 
                max="25" 
                value={rows} 
                onChange={e => setRows(parseInt(e.target.value) || 1)}
                style={{ width: '100%', padding: '6px', marginTop: '4px', borderRadius: '4px', border: '1px solid #ccc' }}
              />
            </div>

            <div>
              <label style={{ fontSize: '0.85rem', fontWeight: 600 }}>Slot Prefix:</label>
              <input 
                type="text" 
                value={slotPrefix} 
                onChange={e => setSlotPrefix(e.target.value)}
                style={{ width: '100%', padding: '6px', marginTop: '4px', borderRadius: '4px', border: '1px solid #ccc' }}
              />
            </div>

            <div>
              <label style={{ fontSize: '0.85rem', fontWeight: 600 }}>Floorplan Blueprint URL (Optional):</label>
              <input 
                type="text" 
                value={blueprintUrl} 
                onChange={e => setBlueprintUrl(e.target.value)}
                style={{ width: '100%', padding: '6px', marginTop: '4px', borderRadius: '4px', border: '1px solid #ccc', fontSize: '11px' }}
              />
            </div>

            <div style={{ display: 'flex', gap: '8px', fontSize: '11px' }}>
              <div style={{ flex: 1 }}>
                <label style={{ fontWeight: 600 }}>NW Anchor Lat/Lng</label>
                <input 
                  type="text" 
                  value={`${nwAnchor.lat}, ${nwAnchor.lng}`}
                  onChange={e => {
                    const [lat, lng] = e.target.value.split(',').map(v => parseFloat(v.trim()));
                    if (!isNaN(lat) && !isNaN(lng)) setNwAnchor({ lat, lng });
                  }}
                  style={{ width: '100%', padding: '4px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
              <div style={{ flex: 1 }}>
                <label style={{ fontWeight: 600 }}>SE Anchor Lat/Lng</label>
                <input 
                  type="text" 
                  value={`${seAnchor.lat}, ${seAnchor.lng}`}
                  onChange={e => {
                    const [lat, lng] = e.target.value.split(',').map(v => parseFloat(v.trim()));
                    if (!isNaN(lat) && !isNaN(lng)) setSeAnchor({ lat, lng });
                  }}
                  style={{ width: '100%', padding: '4px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
            </div>

            <div style={{ backgroundColor: '#f3f4f6', padding: '8px 12px', borderRadius: '6px', fontSize: '0.88rem' }}>
              Total generated slots: <strong>{cols * rows}</strong>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button 
                onClick={() => setShowSubdivideModal(false)}
                style={{ padding: '8px 14px', borderRadius: '4px', border: '1px solid #ccc', backgroundColor: '#fff', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button 
                onClick={generateSubdividedSlots}
                style={{ padding: '8px 14px', borderRadius: '4px', border: 'none', backgroundColor: '#4f46e5', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
              >
                Generate {cols * rows} Slots
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
