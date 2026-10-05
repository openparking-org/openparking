import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useAdminPage } from '../../hooks/useAdminPage';
import { postData, messageOf, type Booking } from '../../services/adminService';
import { Notice, PageHeading, Pagination } from '../../components/PageTools';

export function ReservationsPage() {
  const [params, setParams] = useSearchParams();
  const status = params.get('status') || '';
  const list = useAdminPage<Booking>(`/api/admin/bookings${status ? `?status=${encodeURIComponent(status)}` : ''}`);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const act = async (booking: Booking, action: 'check-in' | 'check-out' | 'cancel' | 'pay') => {
    if (action === 'cancel' && !window.confirm(`Cancel reservation ${booking.id.slice(0, 8)}?`)) return;
    setBusy(booking.id); setError(''); setMessage('');
    try {
      if (action === 'check-in' || action === 'check-out') await postData(`/api/sessions/${action}`, { bookingId: booking.id });
      else await postData(`/api/admin/bookings/${booking.id}/${action}`, {});
      setMessage(action === 'pay' ? 'Payment received at the gate and recorded.' : action === 'check-in' ? 'Vehicle checked in.' : action === 'check-out' ? 'Vehicle checked out. Final charge is ready.' : 'Reservation cancelled.');
      list.reload();
    } catch (err) { setError(messageOf(err)); } finally { setBusy(''); }
  };
  return <><PageHeading title="Reservations & Sessions" description="Manage arrivals, departures, charges, and paid status."><Link to="/book-parking" className="btn btn-primary">Create reservation</Link></PageHeading><Notice error={error || list.error} message={message} />
    <div className="glass-panel admin-panel"><div className="admin-toolbar"><input aria-label="Search reservations" placeholder="Search driver, plate, or bay" value={list.search} onChange={e => list.setSearch(e.target.value)} /><select aria-label="Reservation status" value={status} onChange={e => { setParams(e.target.value ? { status: e.target.value } : {}); list.setPage(1); }}><option value="">All statuses</option>{['Pending', 'Confirmed', 'Active', 'Completed', 'Cancelled', 'Expired'].map(s => <option key={s}>{s}</option>)}</select><button className="btn btn-secondary" onClick={list.reload}>Refresh</button></div>
      {list.loading ? <p role="status">Loading reservations…</p> : !list.error && <div className="admin-table-wrap"><table className="admin-table"><thead><tr><th>Reservation / driver</th><th>Location</th><th>Time window</th><th>Status</th><th>Charge</th><th>Actions</th></tr></thead><tbody>{list.data?.items.map(b => <tr key={b.id}><td><strong title={b.id}>{b.id.slice(0, 8).toUpperCase()}</strong><small>{b.driverName} · {b.vehiclePlate || 'No plate'}</small><small>{b.driverEmail}</small></td><td>{b.zoneName}<small>{b.slotNumber}</small></td><td>{new Date(b.startTime).toLocaleString()}<small>to {new Date(b.endTime).toLocaleString()}</small>{b.session && <small>In: {new Date(b.session.checkInTime).toLocaleString()}{b.session.checkOutTime && <> · Out: {new Date(b.session.checkOutTime).toLocaleString()}</>}</small>}</td><td><span className="status-chip">{b.session?.status === 'OverstayDetected' ? 'Overstay' : b.status}</span></td><td>{b.currency} {(b.session?.checkOutTime ? b.session.totalFee : b.estimatedFee).toFixed(2)}<small>{b.session?.checkOutTime ? 'Final charge' : 'Estimate'}</small>{b.isPaid && <span className="paid-chip">Paid</span>}</td><td><div className="admin-actions">{['Pending', 'Confirmed'].includes(b.status) && <><button className="btn btn-primary" disabled={!!busy} onClick={() => void act(b, 'check-in')}>Check in</button><button className="btn btn-secondary" disabled={!!busy} onClick={() => void act(b, 'cancel')}>Cancel</button></>}{b.status === 'Active' && <button className="btn btn-primary" disabled={!!busy} onClick={() => void act(b, 'check-out')}>Check out</button>}{b.status === 'Completed' && <button className="btn btn-primary" disabled={!!busy || b.isPaid} onClick={() => { if (window.confirm("Confirm that payment has been collected at the gate?")) void act(b, 'pay'); }}>{busy === b.id ? 'Saving…' : b.isPaid ? 'Paid' : 'Record payment'}</button>}</div></td></tr>)}</tbody></table>{list.data?.items.length === 0 && <p>No reservations match your filters.</p>}</div>}
      <Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} />
    </div>
  </>;
}
