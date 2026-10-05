import { useState } from 'react';
import { apiClient } from '../../lib/apiClient';
import { useAdminPage } from '../../hooks/useAdminPage';
import { useAuthStore, type AuthUser } from '../../store/authStore';
import { messageOf } from '../../services/adminService';
import { Notice, PageHeading, Pagination } from '../../components/PageTools';

export function UsersPage() {
  const list = useAdminPage<AuthUser>('/api/users');
  const self = useAuthStore(state => state.user);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState('');
  const [message, setMessage] = useState('');
  const change = async (user: AuthUser, role: string) => {
    if (!window.confirm(`Change ${user.fullName}'s role to ${role}?`)) return;
    setBusy(user.id); setError(''); setMessage('');
    try { await apiClient.patch(`/api/users/${user.id}/role`, { role }); setMessage('Role updated.'); list.reload(); }
    catch (err) { setError(messageOf(err)); } finally { setBusy(''); }
  };
  return <><PageHeading title="User Administration" description="Review accounts and manage admin access." /><Notice error={error || list.error} message={message} /><div className="glass-panel admin-panel"><div className="admin-toolbar"><input aria-label="Search users" placeholder="Search name or email" value={list.search} onChange={e => list.setSearch(e.target.value)} /><button className="btn btn-secondary" onClick={list.reload}>Refresh</button></div>{list.loading ? <p>Loading users…</p> : !list.error && <div className="admin-table-wrap"><table className="admin-table"><thead><tr><th>Name</th><th>Email</th><th>Permit</th><th>Role</th></tr></thead><tbody>{list.data?.items.map(user => <tr key={user.id}><td>{user.fullName}</td><td>{user.email}</td><td>{user.hasDisabilityPermit ? 'Verified' : 'None'}</td><td><select aria-label={`Role for ${user.fullName}`} value={user.role} disabled={!!busy || user.id === self?.id} onChange={e => void change(user, e.target.value)}>{['Driver', 'ParkingAdmin', 'SystemAdmin'].map(role => <option key={role}>{role}</option>)}</select></td></tr>)}</tbody></table>{list.data?.items.length === 0 && <p>No matching users.</p>}</div>}<Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} /></div></>;
}
