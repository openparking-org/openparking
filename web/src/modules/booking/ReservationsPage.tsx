import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useAdminPage } from '../../hooks/useAdminPage';
import { postData, messageOf, type Booking } from '../../services/adminService';
import { EmptyState, Loading, Notice, PageHeading, Pagination, StatusBadge } from '../../components/PageTools';
import { useConfirm } from '../../components/ConfirmDialog';
import { CalendarDays, CalendarPlus, RefreshCw, Search } from 'lucide-react';

export function ReservationsPage() {
  const [params, setParams] = useSearchParams();
  const status = params.get('status') || '';
  const list = useAdminPage<Booking>(`/api/admin/bookings${status ? `?status=${encodeURIComponent(status)}` : ''}`);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const confirm = useConfirm();
  const act = async (booking: Booking, action: 'check-in' | 'check-out' | 'cancel' | 'pay') => {
    if (action === 'cancel' && !await confirm({ title: `Cancel reservation ${booking.id.slice(0, 8).toUpperCase()}?`, body: `${booking.driverName} · ${booking.vehiclePlate || 'No plate'} · ${booking.zoneName} ${booking.slotNumber}. The bay becomes available again.`, confirmLabel: 'Cancel reservation', cancelLabel: 'Keep it', danger: true })) return;
    if (action === 'pay' && !await confirm({ title: 'Record payment?', body: `Confirm that ${booking.currency} ${(booking.session?.totalFee ?? booking.estimatedFee).toFixed(2)} has been collected at the gate.`, confirmLabel: 'Record payment' })) return;
    setBusy(booking.id); setError(''); setMessage('');
    try {
      if (action === 'check-in' || action === 'check-out') await postData(`/api/sessions/${action}`, { bookingId: booking.id });
      else await postData(`/api/admin/bookings/${booking.id}/${action}`, {});
      setMessage(action === 'pay' ? 'Payment received at the gate and recorded.' : action === 'check-in' ? 'Vehicle checked in.' : action === 'check-out' ? 'Vehicle checked out. Final charge is ready.' : 'Reservation cancelled.');
      list.reload();
    } catch (err) { setError(messageOf(err)); } finally { setBusy(''); }
  };
  const time = (value: string) => new Date(value).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
  return <>
    <PageHeading title="Reservations & Sessions" description="Manage arrivals, departures, charges and paid status."><Link to="/book-parking" className="btn btn-primary"><CalendarPlus size={16} aria-hidden="true" />New reservation</Link></PageHeading>
    <Notice error={error || list.error} message={message} />
    <div className="card">
      <div className="toolbar">
        <div className="input-affix"><Search size={16} aria-hidden="true" /><input type="search" aria-label="Search reservations" placeholder="Search driver, plate or bay" value={list.search} onChange={e => list.setSearch(e.target.value)} /></div>
        <div className="segmented" role="group" aria-label="Filter by status">{['', 'Pending', 'Confirmed', 'Active', 'Completed', 'Cancelled', 'Expired'].map(s => <button key={s || 'all'} type="button" aria-pressed={status === s} onClick={() => { setParams(s ? { status: s } : {}); list.setPage(1); }}>{s || 'All'}</button>)}</div>
        <span className="spacer" />
        <button className="btn btn-secondary btn-sm" onClick={list.reload}><RefreshCw size={14} aria-hidden="true" />Refresh</button>
      </div>
      {list.loading ? <Loading label="Loading reservations…" /> : !list.error && (list.data?.items.length === 0
        ? <EmptyState icon={CalendarDays} title="No reservations found" action={<Link to="/book-parking" className="btn btn-secondary">Create a reservation</Link>}>{status || list.search ? 'Nothing matches these filters.' : 'Reservations made by drivers or staff will show here.'}</EmptyState>
        : <div className="table-wrap"><table className="data-table"><thead><tr><th>Reservation</th><th>Location</th><th>Time window</th><th>Status</th><th className="num">Charge</th><th className="actions"><span className="sr-only">Actions</span></th></tr></thead><tbody>{list.data?.items.map(b => {
          const overstay = b.session?.status === 'OverstayDetected';
          const final = !!b.session?.checkOutTime;
          return <tr key={b.id}>
            <td><span className="cell-title mono" title={b.id}>{b.id.slice(0, 8).toUpperCase()}</span><span className="cell-sub">{b.driverName} · {b.vehiclePlate || 'No plate'}</span><span className="cell-sub">{b.driverEmail}</span></td>
            <td><span className="cell-title">{b.zoneName}</span><span className="cell-sub">Bay {b.slotNumber}</span></td>
            <td><span className="tabular">{time(b.startTime)}</span><span className="cell-sub">to {time(b.endTime)}</span>{b.session && <span className="cell-sub">In {time(b.session.checkInTime)}{b.session.checkOutTime && <> · Out {time(b.session.checkOutTime)}</>}</span>}</td>
            <td><StatusBadge status={overstay ? 'Overstay' : b.status} /></td>
            <td className="num"><span className="cell-title">{b.currency} {(final ? b.session!.totalFee : b.estimatedFee).toFixed(2)}</span><span className="cell-sub">{final ? 'Final' : 'Estimate'}{b.isPaid && <> · <span style={{ color: 'var(--good-ink)', fontWeight: 650 }}>Paid</span></>}</span></td>
            <td className="actions"><div>
              {['Pending', 'Confirmed'].includes(b.status) && <><button className="btn btn-primary btn-sm" disabled={!!busy} onClick={() => void act(b, 'check-in')}>{busy === b.id ? 'Saving…' : 'Check in'}</button><button className="btn btn-ghost btn-sm" disabled={!!busy} onClick={() => void act(b, 'cancel')}>Cancel</button></>}
              {b.status === 'Active' && <button className="btn btn-primary btn-sm" disabled={!!busy} onClick={() => void act(b, 'check-out')}>{busy === b.id ? 'Saving…' : 'Check out'}</button>}
              {b.status === 'Completed' && (b.isPaid ? <span className="badge tone-good">Paid</span> : <button className="btn btn-secondary btn-sm" disabled={!!busy} onClick={() => void act(b, 'pay')}>{busy === b.id ? 'Saving…' : 'Record payment'}</button>)}
            </div></td>
          </tr>;
        })}</tbody></table></div>)}
      <Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} />
    </div>
  </>;
}
