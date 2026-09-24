import React from 'react';
import { Calendar, CheckCircle2, Clock, Car } from 'lucide-react';

export const BookingManagementPage: React.FC = () => {
  const mockBookings = [
    { id: 'BK-9912', user: 'driver.john@example.com', slot: 'Zone A (Slot 12)', start: '10:00 AM', end: '12:00 PM', fee: '$10.00', status: 'Active' },
    { id: 'BK-9913', user: 'sarah.m@example.com', slot: 'Zone B (Slot 04)', start: '11:30 AM', end: '01:30 PM', fee: '$12.50', status: 'Confirmed' },
    { id: 'BK-9914', user: 'kevin.p@example.com', slot: 'Zone A (Slot 09)', start: '08:00 AM', end: '09:45 AM', fee: '$8.75', status: 'Completed' },
  ];

  return (
    <div className="glass-panel" style={{ padding: '28px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '24px' }}>
        <div className="brand-icon" style={{ width: '42px', height: '42px', background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)' }}>
          <Calendar size={22} />
        </div>
        <div>
          <h2 style={{ fontSize: '1.35rem', fontWeight: 700 }}>Active Reservations & Sessions</h2>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.88rem' }}>
            Student 3 Slice: Real-time booking lifecycle, automated fee calculation & receipt generation.
          </p>
        </div>
      </div>

      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.9rem' }}>
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-secondary)' }}>
              <th style={{ padding: '12px 16px' }}>Booking ID</th>
              <th style={{ padding: '12px 16px' }}>Driver</th>
              <th style={{ padding: '12px 16px' }}>Slot</th>
              <th style={{ padding: '12px 16px' }}>Time Window</th>
              <th style={{ padding: '12px 16px' }}>Fee</th>
              <th style={{ padding: '12px 16px' }}>Status</th>
            </tr>
          </thead>
          <tbody>
            {mockBookings.map((b) => (
              <tr key={b.id} style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.04)' }}>
                <td style={{ padding: '14px 16px', fontWeight: 600, color: '#818cf8' }}>{b.id}</td>
                <td style={{ padding: '14px 16px' }}>{b.user}</td>
                <td style={{ padding: '14px 16px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <Car size={14} color="#9ca3af" />
                  {b.slot}
                </td>
                <td style={{ padding: '14px 16px', color: 'var(--text-secondary)' }}>{b.start} - {b.end}</td>
                <td style={{ padding: '14px 16px', fontWeight: 600 }}>{b.fee}</td>
                <td style={{ padding: '14px 16px' }}>
                  <span className={`badge ${b.status === 'Active' ? 'badge-success' : b.status === 'Confirmed' ? 'badge-info' : 'badge-warning'}`}>
                    {b.status === 'Active' ? <Clock size={12} style={{ marginRight: '4px' }} /> : <CheckCircle2 size={12} style={{ marginRight: '4px' }} />}
                    {b.status}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};
