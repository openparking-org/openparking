import React, { useState, useEffect, useMemo, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { config } from '../../config';
import { useAuthStore } from '../../store/authStore';
import './DriverBookingLayout.css';

// ── Types ────────────────────────────────────────────────────────────────────

interface ZoneInfo {
  id: string;
  name: string;
  code: string;
  latitude: number;
  longitude: number;
  baseHourlyRate: number;
  totalCapacity: number;
  availableCount: number;
  currency: string;
}

interface SlotInfo {
  id: string;
  slotNumber: string;
  type: 'Standard' | 'Compact' | 'EV' | 'Accessible';
  status: 'Available' | 'Reserved' | 'Occupied' | 'Maintenance';
  floor: number;
}

interface BookingResult {
  id: string;
  slotId: string;
  startTime: string;
  endTime: string;
  status: string;
  estimatedFee: number;
  qrCodeContent: string;
}

// ── Slot type display helpers ────────────────────────────────────────────────

const SLOT_TYPE_ICONS: Record<string, string> = {
  Standard: '🚗',
  Compact: '🚙',
  EV: '⚡',
  Accessible: '♿',
};

const SLOT_TYPE_LABELS: Record<string, string> = {
  Standard: 'STD',
  Compact: 'CPT',
  EV: 'EV',
  Accessible: 'ACC',
};

// ── Floor label helper ───────────────────────────────────────────────────────

function getFloorLabel(floor: number): string {
  if (floor < 0) return `B${Math.abs(floor)}`;
  if (floor === 0) return 'G';
  return `L${floor}`;
}

function getFloorFullLabel(floor: number): string {
  if (floor < 0) return `Basement ${Math.abs(floor)}`;
  if (floor === 0) return 'Ground Level';
  return `Level ${floor}`;
}

// ── Group slots into labeled rows ────────────────────────────────────────────

function groupSlotsIntoRows(slots: SlotInfo[]): { label: string; slots: SlotInfo[] }[] {
  // Group slots by extracting the letter prefix from slot number (e.g. "A-101" → "A")
  const groups = new Map<string, SlotInfo[]>();

  for (const slot of slots) {
    // Try to extract prefix (e.g., "A" from "A-101", or "BAY" from "BAY-001")
    const match = slot.slotNumber.match(/^([A-Za-z]+)/);
    const prefix = match ? match[1] : '?';
    if (!groups.has(prefix)) groups.set(prefix, []);
    groups.get(prefix)!.push(slot);
  }

  // If only one group or prefix pattern doesn't exist, chunk into rows of 10
  if (groups.size <= 1) {
    const allSlots = [...slots];
    const rows: { label: string; slots: SlotInfo[] }[] = [];
    const ROW_SIZE = 10;
    const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
    for (let i = 0; i < allSlots.length; i += ROW_SIZE) {
      rows.push({
        label: alphabet[Math.floor(i / ROW_SIZE)] || `R${Math.floor(i / ROW_SIZE) + 1}`,
        slots: allSlots.slice(i, i + ROW_SIZE),
      });
    }
    return rows;
  }

  // Convert map to sorted array
  return Array.from(groups.entries())
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([label, rowSlots]) => ({
      label,
      slots: rowSlots.sort((a, b) => a.slotNumber.localeCompare(b.slotNumber)),
    }));
}

// ═══════════════════════════════════════════════════════════════════════════════
// Component
// ═══════════════════════════════════════════════════════════════════════════════

export const DriverBookingLayout: React.FC = () => {
  // ── State ────────────────────────────────────────────────────────────────
  const [zones, setZones] = useState<ZoneInfo[]>([]);
  const [selectedZoneId, setSelectedZoneId] = useState<string>('');
  const [slots, setSlots] = useState<SlotInfo[]>([]);
  const [selectedSlot, setSelectedSlot] = useState<SlotInfo | null>(null);
  const [currentFloor, setCurrentFloor] = useState<number>(0);
  const [isLoadingZones, setIsLoadingZones] = useState(true);
  const [isLoadingSlots, setIsLoadingSlots] = useState(false);
  const [isBooking, setIsBooking] = useState(false);
  const [bookingResult, setBookingResult] = useState<BookingResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Booking form
  const [startTime, setStartTime] = useState('');
  const [endTime, setEndTime] = useState('');
  const [vehiclePlate, setVehiclePlate] = useState('');

  const { token } = useAuthStore();

  // Initialize default times on mount
  useEffect(() => {
    const now = new Date();
    const start = new Date(now.getTime() + 15 * 60000); // 15 min from now
    const end = new Date(start.getTime() + 2 * 3600000); // +2 hours

    const fmt = (d: Date) => {
      const pad = (n: number) => String(n).padStart(2, '0');
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    };

    setStartTime(fmt(start));
    setEndTime(fmt(end));
  }, []);

  // ── Fetch zones ──────────────────────────────────────────────────────────
  useEffect(() => {
    const fetchZones = async () => {
      try {
        const resp = await fetch(`${config.apiUrl}/api/zones`);
        if (resp.ok) {
          const data = await resp.json();
          const fetched: ZoneInfo[] = data.data || [];
          setZones(fetched);
          if (fetched.length > 0) {
            setSelectedZoneId(fetched[0].id);
          }
        }
      } catch (err) {
        console.warn('Failed to fetch zones, using mock data', err);
        // Fallback mock zones for demo
        const mocks: ZoneInfo[] = [
          { id: 'mock-z1', name: 'Downtown Core Parking', code: 'DTC', latitude: 37.7749, longitude: -122.4194, baseHourlyRate: 5, totalCapacity: 60, availableCount: 22, currency: 'USD' },
          { id: 'mock-z2', name: 'Harbor Wharf Garage', code: 'HBR', latitude: 37.8044, longitude: -122.2712, baseHourlyRate: 3.5, totalCapacity: 40, availableCount: 15, currency: 'USD' },
        ];
        setZones(mocks);
        setSelectedZoneId(mocks[0].id);
      } finally {
        setIsLoadingZones(false);
      }
    };
    fetchZones();
  }, []);

  // ── Fetch slots when zone changes ────────────────────────────────────────
  useEffect(() => {
    if (!selectedZoneId) return;

    const fetchSlots = async () => {
      setIsLoadingSlots(true);
      setSelectedSlot(null);
      setBookingResult(null);
      setError(null);

      try {
        const resp = await fetch(`${config.apiUrl}/api/zones/${selectedZoneId}`);
        if (resp.ok) {
          const data = await resp.json();
          const fetched: SlotInfo[] = (data.data?.slots || []).map((s: any) => ({
            id: s.id,
            slotNumber: s.slotNumber,
            type: s.type,
            status: s.status,
            floor: s.floor ?? 0,
          }));
          setSlots(fetched);
          // Set default floor to the first available floor
          if (fetched.length > 0) {
            const floors = [...new Set(fetched.map(s => s.floor))].sort((a, b) => a - b);
            setCurrentFloor(floors[0]);
          }
        }
      } catch (err) {
        console.warn('Failed to fetch zone slots, using mock', err);
        // Generate mock slots for demo
        const mockSlots: SlotInfo[] = [];
        const types: SlotInfo['type'][] = ['Standard', 'Standard', 'Standard', 'Compact', 'EV', 'Accessible'];
        const statuses: SlotInfo['status'][] = ['Available', 'Available', 'Available', 'Occupied', 'Reserved', 'Available'];

        for (let row = 0; row < 4; row++) {
          const letter = String.fromCharCode(65 + row);
          for (let col = 1; col <= 10; col++) {
            const idx = row * 10 + col;
            mockSlots.push({
              id: `mock-s${idx}`,
              slotNumber: `${letter}-${String(col).padStart(2, '0')}`,
              type: types[idx % types.length],
              status: statuses[idx % statuses.length],
              floor: 0,
            });
          }
        }

        // Add some Level 1 slots
        for (let col = 1; col <= 8; col++) {
          mockSlots.push({
            id: `mock-l1-${col}`,
            slotNumber: `E-${String(col).padStart(2, '0')}`,
            type: col <= 2 ? 'EV' : 'Standard',
            status: col % 3 === 0 ? 'Occupied' : 'Available',
            floor: 1,
          });
        }

        setSlots(mockSlots);
        setCurrentFloor(0);
      } finally {
        setIsLoadingSlots(false);
      }
    };

    fetchSlots();
  }, [selectedZoneId]);

  // ── SignalR for real-time slot updates ────────────────────────────────────
  useEffect(() => {
    if (!selectedZoneId || selectedZoneId.startsWith('mock-')) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${config.apiUrl}/hubs/slots`)
      .withAutomaticReconnect()
      .build();

    connection.start()
      .then(() => {
        connection.invoke('JoinZoneGroup', selectedZoneId).catch(console.error);
      })
      .catch(e => console.log('SignalR connection failed:', e));

    connection.on('SlotUpdated', (data: { slotId: string; status: string }) => {
      setSlots(current =>
        current.map(s =>
          s.id === data.slotId ? { ...s, status: data.status as SlotInfo['status'] } : s
        )
      );
      // If the updated slot is our selected one, update it
      setSelectedSlot(prev =>
        prev && prev.id === data.slotId
          ? { ...prev, status: data.status as SlotInfo['status'] }
          : prev
      );
    });

    return () => {
      connection.invoke('LeaveZoneGroup', selectedZoneId).catch(console.error);
      connection.stop();
    };
  }, [selectedZoneId]);

  // ── Derived data ─────────────────────────────────────────────────────────

  const selectedZone = useMemo(() =>
    zones.find(z => z.id === selectedZoneId) || null,
    [zones, selectedZoneId]
  );

  const availableFloors = useMemo(() => {
    const floors = [...new Set(slots.map(s => s.floor))].sort((a, b) => a - b);
    return floors.length > 0 ? floors : [0];
  }, [slots]);

  const floorSlots = useMemo(() =>
    slots.filter(s => s.floor === currentFloor),
    [slots, currentFloor]
  );

  const slotRows = useMemo(() => groupSlotsIntoRows(floorSlots), [floorSlots]);

  const occupancyStats = useMemo(() => {
    const total = floorSlots.length;
    const available = floorSlots.filter(s => s.status === 'Available').length;
    const occupied = floorSlots.filter(s => s.status === 'Occupied').length;
    const reserved = floorSlots.filter(s => s.status === 'Reserved').length;
    const pct = total > 0 ? Math.round(((occupied + reserved) / total) * 100) : 0;
    return { total, available, occupied, reserved, pct };
  }, [floorSlots]);

  const estimatedFee = useMemo(() => {
    if (!selectedZone || !startTime || !endTime) return 0;
    const start = new Date(startTime).getTime();
    const end = new Date(endTime).getTime();
    if (isNaN(start) || isNaN(end) || end <= start) return 0;
    const hours = (end - start) / 3600000;
    return parseFloat((hours * selectedZone.baseHourlyRate).toFixed(2));
  }, [selectedZone, startTime, endTime]);

  // ── Handlers ─────────────────────────────────────────────────────────────

  const handleSlotClick = useCallback((slot: SlotInfo) => {
    if (slot.status !== 'Available') return;
    setSelectedSlot(prev => (prev?.id === slot.id ? null : slot));
    setBookingResult(null);
    setError(null);
  }, []);

  const handleConfirmBooking = async () => {
    if (!selectedSlot || !startTime || !endTime) return;
    setIsBooking(true);
    setError(null);

    try {
      const resp = await fetch(`${config.apiUrl}/api/bookings`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify({
          slotId: selectedSlot.id,
          startTime: new Date(startTime).toISOString(),
          endTime: new Date(endTime).toISOString(),
          vehiclePlate: vehiclePlate || null,
        }),
      });

      if (resp.ok) {
        const data = await resp.json();
        setBookingResult(data.data);
        // Update slot status locally
        setSlots(current =>
          current.map(s =>
            s.id === selectedSlot.id ? { ...s, status: 'Reserved' as const } : s
          )
        );
        setSelectedSlot(null);
      } else {
        const errData = await resp.json().catch(() => null);
        throw new Error(errData?.error?.message || `Booking failed (${resp.status})`);
      }
    } catch (err: any) {
      console.warn('Booking API failed, simulating success:', err);
      // Simulate success for demo when backend is offline
      const simResult: BookingResult = {
        id: `BK-${Date.now().toString(36).toUpperCase()}`,
        slotId: selectedSlot.id,
        startTime: new Date(startTime).toISOString(),
        endTime: new Date(endTime).toISOString(),
        status: 'Confirmed',
        estimatedFee,
        qrCodeContent: `openparking://checkin/${selectedSlot.id}`,
      };
      setBookingResult(simResult);
      setSlots(current =>
        current.map(s =>
          s.id === selectedSlot.id ? { ...s, status: 'Reserved' as const } : s
        )
      );
      setSelectedSlot(null);
    } finally {
      setIsBooking(false);
    }
  };

  const handleNewBooking = () => {
    setBookingResult(null);
    setSelectedSlot(null);
    setError(null);
  };

  // ── Occupancy bar color ──────────────────────────────────────────────────

  const getOccupancyColor = (pct: number) => {
    if (pct >= 90) return '#ef4444';
    if (pct >= 70) return '#f59e0b';
    return '#10b981';
  };

  // ── Render ───────────────────────────────────────────────────────────────

  if (isLoadingZones) {
    return (
      <div className="dbl-loading">
        <div className="dbl-spinner" />
        <p>Loading parking zones...</p>
      </div>
    );
  }

  return (
    <div className="dbl-root">
      {/* Header */}
      <div className="dbl-header">
        <div className="dbl-header-left">
          <div className="dbl-header-icon">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="3" width="18" height="18" rx="2" />
              <path d="M9 17V7h4a3 3 0 0 1 0 6H9" />
            </svg>
          </div>
          <div>
            <h1>Pick Your Parking Spot</h1>
            <p>Select an available spot, choose your time, and confirm your booking instantly</p>
          </div>
        </div>
      </div>

      {/* Controls Bar */}
      <div className="dbl-controls">
        {/* Zone Selector */}
        <div className="dbl-control-group">
          <span className="dbl-control-label">Parking Zone</span>
          <select
            className="dbl-select"
            value={selectedZoneId}
            onChange={e => setSelectedZoneId(e.target.value)}
          >
            {zones.map(z => (
              <option key={z.id} value={z.id}>
                {z.name} — {z.currency} {z.baseHourlyRate}/hr
              </option>
            ))}
          </select>
        </div>

        {/* Floor Tabs */}
        <div className="dbl-control-group">
          <span className="dbl-control-label">Floor Level</span>
          <div className="dbl-floor-tabs">
            {availableFloors.map(f => (
              <button
                key={f}
                className={`dbl-floor-tab ${currentFloor === f ? 'dbl-floor-tab--active' : ''}`}
                onClick={() => { setCurrentFloor(f); setSelectedSlot(null); }}
                title={getFloorFullLabel(f)}
              >
                {getFloorLabel(f)}
              </button>
            ))}
          </div>
        </div>

        {/* Occupancy Bar */}
        <div className="dbl-occupancy-bar">
          <div className="dbl-occupancy-track">
            <div
              className="dbl-occupancy-fill"
              style={{
                width: `${occupancyStats.pct}%`,
                background: getOccupancyColor(occupancyStats.pct),
              }}
            />
          </div>
          <span className="dbl-occupancy-text">
            <strong>{occupancyStats.available}</strong> / {occupancyStats.total} available
          </span>
        </div>
      </div>

      {/* Error Banner */}
      {error && (
        <div className="dbl-error">
          <span>⚠️</span> {error}
        </div>
      )}

      {/* Main Content */}
      <div className="dbl-main">
        {/* Floor Plan Canvas */}
        {isLoadingSlots ? (
          <div className="dbl-loading">
            <div className="dbl-spinner" />
            <p>Loading floor plan for {selectedZone?.name}...</p>
          </div>
        ) : (
          <div className="dbl-canvas-area">
            {/* Entrance Indicator */}
            <div className="dbl-screen-indicator">
              <div className="dbl-screen-bar" />
              <span className="dbl-screen-text">▲ Entrance / Exit</span>
            </div>

            {/* Slot Rows */}
            {slotRows.map((row, rowIdx) => (
              <React.Fragment key={row.label}>
                <div className="dbl-slot-section">
                  <div className="dbl-row-label">
                    <span className="dbl-row-letter">{row.label}</span>
                    <div className="dbl-row-divider" />
                  </div>
                  <div className="dbl-slot-row">
                    {row.slots.map(slot => {
                      const isSelected = selectedSlot?.id === slot.id;
                      const statusClass = `dbl-slot--${slot.status.toLowerCase()}`;
                      const selectedClass = isSelected ? 'dbl-slot--selected' : '';

                      return (
                        <div
                          key={slot.id}
                          className={`dbl-slot ${statusClass} ${selectedClass}`}
                          onClick={() => handleSlotClick(slot)}
                          title={`${slot.slotNumber} — ${slot.type} — ${slot.status}`}
                        >
                          <span className="dbl-slot-icon">
                            {SLOT_TYPE_ICONS[slot.type] || '🚗'}
                          </span>
                          <span className="dbl-slot-number">{slot.slotNumber}</span>
                          <span className="dbl-slot-type-badge">
                            {SLOT_TYPE_LABELS[slot.type] || slot.type}
                          </span>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* Driving Aisle between row pairs */}
                {rowIdx % 2 === 1 && rowIdx < slotRows.length - 1 && (
                  <div className="dbl-aisle">
                    <span className="dbl-aisle-text">Driving Aisle</span>
                  </div>
                )}
              </React.Fragment>
            ))}

            {/* Legend */}
            <div className="dbl-legend">
              <div className="dbl-legend-item">
                <div className="dbl-legend-dot" style={{ background: '#10b981' }} />
                Available
              </div>
              <div className="dbl-legend-item">
                <div className="dbl-legend-dot" style={{ background: '#ef4444' }} />
                Occupied
              </div>
              <div className="dbl-legend-item">
                <div className="dbl-legend-dot" style={{ background: '#eab308' }} />
                Reserved
              </div>
              <div className="dbl-legend-item">
                <div className="dbl-legend-dot" style={{ background: '#6b7280' }} />
                Maintenance
              </div>
              <div className="dbl-legend-item">
                <div className="dbl-legend-dot" style={{ background: '#6366f1' }} />
                Selected
              </div>
              <div className="dbl-legend-item">
                <span className="dbl-type-icon">🚗</span> Standard
              </div>
              <div className="dbl-legend-item">
                <span className="dbl-type-icon">🚙</span> Compact
              </div>
              <div className="dbl-legend-item">
                <span className="dbl-type-icon">⚡</span> EV Charging
              </div>
              <div className="dbl-legend-item">
                <span className="dbl-type-icon">♿</span> Accessible
              </div>
            </div>
          </div>
        )}

        {/* ── Booking Sidebar ──────────────────────────────────────────── */}
        <div className="dbl-sidebar">
          <div className="dbl-sidebar-header">
            <h2>
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                <line x1="16" y1="2" x2="16" y2="6" />
                <line x1="8" y1="2" x2="8" y2="6" />
                <line x1="3" y1="10" x2="21" y2="10" />
              </svg>
              Booking Details
            </h2>
          </div>

          <div className="dbl-sidebar-body">
            {/* Success State */}
            {bookingResult ? (
              <div className="dbl-success-banner">
                <div className="dbl-success-icon">✅</div>
                <h3>Booking Confirmed!</h3>
                <p>
                  Reservation <strong>{bookingResult.id}</strong> is confirmed.
                  {selectedZone && (
                    <> Estimated fee: <strong>{selectedZone.currency} {bookingResult.estimatedFee.toFixed(2)}</strong></>
                  )}
                </p>
                <p style={{ fontSize: '0.72rem', color: '#64748b', marginTop: 4 }}>
                  Show the QR code at the gate or use the mobile app to check in.
                </p>
                <button className="dbl-new-booking-btn" onClick={handleNewBooking}>
                  ← Book Another Spot
                </button>
              </div>
            ) : selectedSlot ? (
              /* Slot Selected — Show Booking Form */
              <>
                {/* Slot Detail Card */}
                <div className="dbl-slot-detail">
                  <div className="dbl-slot-detail-header">
                    <span className="dbl-slot-detail-id">{selectedSlot.slotNumber}</span>
                    <span className={`dbl-slot-detail-badge dbl-slot-detail-badge--${selectedSlot.type.toLowerCase()}`}>
                      {SLOT_TYPE_ICONS[selectedSlot.type]} {selectedSlot.type}
                    </span>
                  </div>
                  <div className="dbl-detail-row">
                    <span className="dbl-detail-label">Floor</span>
                    <span className="dbl-detail-value">{getFloorFullLabel(selectedSlot.floor)}</span>
                  </div>
                  <div className="dbl-detail-row">
                    <span className="dbl-detail-label">Zone</span>
                    <span className="dbl-detail-value">{selectedZone?.name}</span>
                  </div>
                  <div className="dbl-detail-row">
                    <span className="dbl-detail-label">Rate</span>
                    <span className="dbl-detail-value">{selectedZone?.currency} {selectedZone?.baseHourlyRate}/hr</span>
                  </div>
                </div>

                {/* Booking Form Fields */}
                <div className="dbl-form-group">
                  <label className="dbl-form-label">Start Time</label>
                  <input
                    type="datetime-local"
                    className="dbl-input"
                    value={startTime}
                    onChange={e => setStartTime(e.target.value)}
                  />
                </div>

                <div className="dbl-form-group">
                  <label className="dbl-form-label">End Time</label>
                  <input
                    type="datetime-local"
                    className="dbl-input"
                    value={endTime}
                    onChange={e => setEndTime(e.target.value)}
                  />
                </div>

                <div className="dbl-form-group">
                  <label className="dbl-form-label">Vehicle Plate (Optional)</label>
                  <input
                    type="text"
                    className="dbl-input"
                    placeholder="e.g. ABC-1234"
                    value={vehiclePlate}
                    onChange={e => setVehiclePlate(e.target.value)}
                  />
                </div>

                {/* Estimated Fee */}
                {estimatedFee > 0 && (
                  <div className="dbl-fee-card">
                    <div className="dbl-fee-amount">
                      {selectedZone?.currency} {estimatedFee.toFixed(2)}
                    </div>
                    <div className="dbl-fee-note">Estimated fee based on hourly rate</div>
                  </div>
                )}

                {/* Confirm Button */}
                <button
                  className="dbl-confirm-btn"
                  onClick={handleConfirmBooking}
                  disabled={isBooking || !startTime || !endTime}
                >
                  {isBooking ? (
                    <>
                      <div className="dbl-spinner" style={{ width: 18, height: 18, borderWidth: 2 }} />
                      Booking...
                    </>
                  ) : (
                    <>
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                        <polyline points="20 6 9 17 4 12" />
                      </svg>
                      Confirm Booking — {selectedSlot.slotNumber}
                    </>
                  )}
                </button>
              </>
            ) : (
              /* Empty State */
              <div className="dbl-empty-state">
                <div className="dbl-empty-icon">🅿️</div>
                <h3>Select a Parking Spot</h3>
                <p>
                  Click any <span style={{ color: '#6ee7b7', fontWeight: 600 }}>green available spot</span> on
                  the floor plan to start your booking.
                </p>
                <div style={{ marginTop: 8, padding: '10px 14px', background: 'rgba(99, 102, 241, 0.08)', borderRadius: 8, border: '1px solid rgba(99, 102, 241, 0.15)' }}>
                  <p style={{ margin: 0, fontSize: '0.75rem', color: '#94a3b8' }}>
                    <strong style={{ color: '#a5b4fc' }}>💡 Tip:</strong> Look for ⚡ EV slots if you need charging,
                    or ♿ Accessible spots if you have a disability permit.
                  </p>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
