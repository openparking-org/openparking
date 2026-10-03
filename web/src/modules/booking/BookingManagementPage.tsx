import React, { useCallback } from 'react';
import { AlertCircle, Calendar, Car, CheckCircle2, Clock, Loader2, RefreshCw } from 'lucide-react';
import { usePaginatedQuery } from '../../hooks/usePaginatedQuery';
import { fetchBookings, type Booking, type BookingStatus } from './bookingApi';

const PAGE_SIZE = 10;

const currency = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });
const timeOnly = new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit' });
const dayAndTime = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });

/** Reservations spanning more than one day need the date to stay unambiguous. */
function formatWindow(startIso: string, endIso: string): string {
  const start = new Date(startIso);
  const end = new Date(endIso);
  const sameDay = start.toDateString() === end.toDateString();

  return sameDay
    ? `${dayAndTime.format(start)} – ${timeOnly.format(end)}`
    : `${dayAndTime.format(start)} – ${dayAndTime.format(end)}`;
}

function badgeClassFor(status: BookingStatus): string {
  switch (status) {
    case 'Active': return 'badge badge-success';
    case 'Confirmed': return 'badge badge-info';
    case 'Cancelled':
    case 'Expired': return 'badge badge-danger';
    default: return 'badge badge-warning';
  }
}

export const BookingManagementPage: React.FC = () => {
  const fetchPage = useCallback(
    (page: number, pageSize: number) => fetchBookings(page, pageSize),
    []
  );

  const { items, totalCount, totalPages, page, setPage, isLoading, error, reload } =
    usePaginatedQuery<Booking>(fetchPage, 1, PAGE_SIZE);

  return (
    <div className="glass-panel" style={{ padding: '28px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '24px' }}>
        <div className="brand-icon" style={{ width: '42px', height: '42px', background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)' }}>
          <Calendar size={22} />
        </div>
        <div style={{ flex: 1 }}>
          <h2 style={{ fontSize: '1.35rem', fontWeight: 700 }}>Active Reservations &amp; Sessions</h2>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.88rem' }}>
            Booking &amp; Payment slice: reservation lifecycle, settings-driven fee calculation &amp; receipts.
          </p>
        </div>
        <button
          onClick={reload}
          disabled={isLoading}
          style={{
            display: 'flex', alignItems: 'center', gap: '6px', padding: '8px 14px',
            borderRadius: 'var(--radius-md)', border: '1px solid var(--border-color)',
            background: 'rgba(255,255,255,0.03)', color: 'var(--text-secondary)',
            fontSize: '0.82rem', cursor: isLoading ? 'default' : 'pointer',
          }}
        >
          <RefreshCw size={14} className={isLoading ? 'spin' : undefined} />
          Refresh
        </button>
      </div>

      {error && (
        <div
          role="alert"
          style={{
            display: 'flex', alignItems: 'center', gap: '10px', padding: '14px 16px',
            marginBottom: '18px', borderRadius: 'var(--radius-md)',
            border: '1px solid rgba(239, 68, 68, 0.35)', background: 'rgba(239, 68, 68, 0.08)',
          }}
        >
          <AlertCircle size={18} color="#f87171" />
          <div style={{ fontSize: '0.86rem' }}>
            <strong>Could not load bookings.</strong>{' '}
            <span style={{ color: 'var(--text-secondary)' }}>{error.message}</span>
          </div>
        </div>
      )}

      {isLoading && items.length === 0 && (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '10px', padding: '48px', color: 'var(--text-secondary)' }}>
          <Loader2 size={18} className="spin" />
          Loading reservations…
        </div>
      )}

      {!isLoading && !error && items.length === 0 && (
        <div style={{ textAlign: 'center', padding: '48px', color: 'var(--text-secondary)' }}>
          <Car size={32} style={{ opacity: 0.4, marginBottom: '10px' }} />
          <p style={{ fontWeight: 600, marginBottom: '4px' }}>No reservations yet</p>
          <p style={{ fontSize: '0.85rem' }}>Bookings created through the driver app will appear here.</p>
        </div>
      )}

      {items.length > 0 && (
        <>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.9rem' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-secondary)' }}>
                  <th style={{ padding: '12px 16px' }}>Booking</th>
                  <th style={{ padding: '12px 16px' }}>Slot</th>
                  <th style={{ padding: '12px 16px' }}>Time Window</th>
                  <th style={{ padding: '12px 16px' }}>Est. Fee</th>
                  <th style={{ padding: '12px 16px' }}>Status</th>
                </tr>
              </thead>
              <tbody>
                {items.map((booking) => (
                  <tr key={booking.id} style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.04)' }}>
                    <td style={{ padding: '14px 16px', fontWeight: 600, color: '#818cf8', fontFamily: 'ui-monospace, monospace', fontSize: '0.82rem' }}>
                      {booking.id.slice(0, 8)}
                    </td>
                    <td style={{ padding: '14px 16px' }}>
                      <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                        <Car size={14} color="#9ca3af" />
                        {booking.zoneName} ({booking.slotNumber})
                      </span>
                    </td>
                    <td style={{ padding: '14px 16px', color: 'var(--text-secondary)' }}>
                      {formatWindow(booking.startTime, booking.endTime)}
                    </td>
                    <td style={{ padding: '14px 16px', fontWeight: 600 }}>
                      {currency.format(booking.estimatedFee)}
                    </td>
                    <td style={{ padding: '14px 16px' }}>
                      <span className={badgeClassFor(booking.status)}>
                        {booking.status === 'Active'
                          ? <Clock size={12} style={{ marginRight: '4px' }} />
                          : <CheckCircle2 size={12} style={{ marginRight: '4px' }} />}
                        {booking.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: '18px', fontSize: '0.84rem', color: 'var(--text-secondary)' }}>
            <span>
              Showing {items.length} of {totalCount} reservation{totalCount === 1 ? '' : 's'}
            </span>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <button
                onClick={() => setPage(page - 1)}
                disabled={page <= 1 || isLoading}
                style={pagerStyle(page <= 1 || isLoading)}
              >
                Previous
              </button>
              <span>Page {page} of {totalPages}</span>
              <button
                onClick={() => setPage(page + 1)}
                disabled={page >= totalPages || isLoading}
                style={pagerStyle(page >= totalPages || isLoading)}
              >
                Next
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

function pagerStyle(disabled: boolean): React.CSSProperties {
  return {
    padding: '6px 12px',
    borderRadius: 'var(--radius-md)',
    border: '1px solid var(--border-color)',
    background: 'rgba(255,255,255,0.03)',
    color: disabled ? 'var(--text-muted)' : 'var(--text-secondary)',
    cursor: disabled ? 'default' : 'pointer',
    opacity: disabled ? 0.5 : 1,
  };
}
