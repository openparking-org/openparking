import { useState } from 'react';
import { apiClient } from '../../lib/apiClient';
import { useAdminPage } from '../../hooks/useAdminPage';
import { postData, messageOf } from '../../services/adminService';
import { Notice, PageHeading, Pagination } from '../../components/PageTools';

interface Permit { id: string; fullName: string; permitNumber: string; jurisdiction: string; expiryDate: string; documentImageUrl: string; status: string; createdAt: string; reviewedAt?: string; reviewNotes?: string; rejectionReason?: string }
interface Validation { valid: boolean; confidence: number; reason?: string; issues?: string[] }
function documentUrl(value: string): string | undefined { try { const url = new URL(value); return ['http:', 'https:'].includes(url.protocol) ? value : undefined; } catch { return undefined; } }
export function PermitReviewPage() {
  const [filter, setFilter] = useState('Pending');
  const list = useAdminPage<Permit>(`/api/admin/permits${filter ? `?status=${filter}` : ''}`);
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [results, setResults] = useState<Record<string, Validation>>({});
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const validate = async (permit: Permit) => {
    setBusy(permit.id); setError('');
    try { const result = await postData<Validation>('/api/admin/ai/permit-validation', { permit_number: permit.permitNumber, expiry_date: permit.expiryDate, jurisdiction: permit.jurisdiction, document_image_url: permit.documentImageUrl }); setResults(current => ({ ...current, [permit.id]: result })); }
    catch (err) { setError(messageOf(err)); } finally { setBusy(''); }
  };
  const review = async (permit: Permit, decision: 'Verified' | 'Rejected') => {
    const reason = notes[permit.id]?.trim() || '';
    if (decision === 'Rejected' && !reason) { setError('Enter a rejection reason.'); return; }
    if (decision === 'Verified' && new Date(permit.expiryDate).getTime() <= Date.now()) { setError('An expired permit cannot be verified.'); return; }
    setBusy(permit.id); setError(''); setMessage('');
    try { await apiClient.patch(`/api/users/permits/${permit.id}/review`, { decision, notes: reason || 'Manually verified by admin' }); setMessage(decision === 'Verified' ? 'Permit verified.' : 'Permit rejected.'); list.reload(); }
    catch (err) { setError(messageOf(err)); list.reload(); } finally { setBusy(''); }
  };
  return <><PageHeading title="Disability Permit Review" description="Inspect documents, request an AI recommendation, and record your decision."><select aria-label="Permit status" value={filter} onChange={e => { setFilter(e.target.value); list.setPage(1); }}><option value="">All statuses</option>{['Pending', 'Verified', 'Rejected', 'Expired'].map(s => <option key={s}>{s}</option>)}</select><button className="btn btn-secondary" onClick={list.reload}>Refresh</button></PageHeading><Notice error={error || list.error} message={message} />
    {list.loading ? <p role="status">Loading permits…</p> : !list.error && <>{list.data?.items.map(permit => <article className="glass-panel admin-panel" key={permit.id}><div className="admin-toolbar"><h2>{permit.fullName}</h2><span className="status-chip">{permit.status}</span></div><div className="admin-form-grid"><div><p>Permit: <strong>{permit.permitNumber}</strong></p><p>Jurisdiction: {permit.jurisdiction}</p><p>Expiry: {new Date(permit.expiryDate).toLocaleDateString()}</p><p>Submitted: {new Date(permit.createdAt).toLocaleString()}</p>{permit.reviewedAt && <p>Reviewed: {new Date(permit.reviewedAt).toLocaleString()}</p>}{permit.reviewNotes && <p>Notes: {permit.reviewNotes}</p>}{permit.rejectionReason && <p>Rejection: {permit.rejectionReason}</p>}</div><div>{documentUrl(permit.documentImageUrl) ? <a className="btn btn-secondary" href={documentUrl(permit.documentImageUrl)} target="_blank" rel="noopener noreferrer">Open permit document</a> : <p>Document is unavailable or has an unsupported URL.</p>}{results[permit.id] && <div className="admin-notice"><strong>AI recommendation: {results[permit.id].valid ? 'Valid metadata' : 'Needs attention'}</strong><p>Confidence: {(results[permit.id].confidence * 100).toFixed(0)}%</p><p>{results[permit.id].reason || 'Verify the document before approving.'}</p></div>}</div></div>{permit.status === 'Pending' && <><label>Review notes / rejection reason<textarea maxLength={1000} value={notes[permit.id] || ''} onChange={e => setNotes(current => ({ ...current, [permit.id]: e.target.value }))} /></label><div className="admin-actions"><button className="btn btn-secondary" disabled={!!busy} onClick={() => void validate(permit)}>Run AI validation</button><button className="btn btn-primary" disabled={!!busy} onClick={() => void review(permit, 'Verified')}>Verify permit</button><button className="btn btn-secondary" disabled={!!busy || !notes[permit.id]?.trim()} onClick={() => void review(permit, 'Rejected')}>Reject</button></div></>}</article>)}{list.data?.items.length === 0 && <div className="glass-panel admin-panel">No permits match this status.</div>}</>}
    <Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} />
  </>;
}
