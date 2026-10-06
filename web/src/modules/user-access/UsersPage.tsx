import { useState } from 'react';
import { apiClient } from '../../lib/apiClient';
import { useAdminPage } from '../../hooks/useAdminPage';
import { useAuthStore, type AuthUser } from '../../store/authStore';
import { messageOf } from '../../services/adminService';
import { EmptyState, Loading, Notice, PageHeading, Pagination } from '../../components/PageTools';
import { useConfirm } from '../../components/ConfirmDialog';
import { RefreshCw, Search, Users } from 'lucide-react';

const roleLabel: Record<string, string> = { Driver: 'Driver', ParkingAdmin: 'Parking admin', SystemAdmin: 'System admin' };

export function UsersPage() {
  const list = useAdminPage<AuthUser>('/api/users');
  const self = useAuthStore(state => state.user);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState('');
  const [message, setMessage] = useState('');
  const confirm = useConfirm();
  const change = async (user: AuthUser, role: string) => {
    if (!await confirm({ title: `Change ${user.fullName}’s role?`, body: `${roleLabel[user.role] || user.role} → ${roleLabel[role] || role}. ${role === 'Driver' ? 'They will lose access to this admin console.' : 'They will be able to sign in to this admin console.'}`, confirmLabel: 'Change role', danger: role === 'SystemAdmin' || user.role === 'SystemAdmin' })) return;
    setBusy(user.id); setError(''); setMessage('');
    try { await apiClient.patch(`/api/users/${user.id}/role`, { role }); setMessage('Role updated.'); list.reload(); }
    catch (err) { setError(messageOf(err)); } finally { setBusy(''); }
  };
  const initials = (name: string) => name.split(' ').filter(Boolean).map(part => part[0]).slice(0, 2).join('').toUpperCase();
  return <>
    <PageHeading title="User Administration" description="Review accounts and manage who can access the admin console." />
    <Notice error={error || list.error} message={message} />
    <div className="card">
      <div className="toolbar"><div className="input-affix"><Search size={16} aria-hidden="true" /><input type="search" aria-label="Search users" placeholder="Search name or email" value={list.search} onChange={e => list.setSearch(e.target.value)} /></div><span className="spacer" /><button className="btn btn-secondary btn-sm" onClick={list.reload}><RefreshCw size={14} aria-hidden="true" />Refresh</button></div>
      {list.loading ? <Loading label="Loading users…" /> : !list.error && (list.data?.items.length === 0
        ? <EmptyState icon={Users} title="No matching users">Try a different name or email.</EmptyState>
        : <div className="table-wrap"><table className="data-table"><thead><tr><th>User</th><th>Disability permit</th><th>Role</th></tr></thead><tbody>{list.data?.items.map(user => <tr key={user.id}>
          <td><div className="cell-person"><span className="avatar" aria-hidden="true">{initials(user.fullName)}</span><div><span className="cell-title">{user.fullName}{user.id === self?.id && <span className="badge no-dot" style={{ marginLeft: 8 }}>You</span>}</span><span className="cell-sub">{user.email}</span></div></div></td>
          <td>{user.hasDisabilityPermit ? <span className="badge tone-good">Verified</span> : <span className="subtle">None</span>}</td>
          <td style={{ width: 220 }}><select aria-label={`Role for ${user.fullName}`} value={user.role} disabled={!!busy || user.id === self?.id} title={user.id === self?.id ? 'You can’t change your own role' : undefined} onChange={e => void change(user, e.target.value)}>{['Driver', 'ParkingAdmin', 'SystemAdmin'].map(role => <option key={role} value={role}>{roleLabel[role]}</option>)}</select></td>
        </tr>)}</tbody></table></div>)}
      <Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} />
    </div>
  </>;
}
