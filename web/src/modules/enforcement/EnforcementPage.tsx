import { useContext, useState } from 'react';
import { SettingsContext } from '../../App';
import { useAdminPage } from '../../hooks/useAdminPage';
import { getData, postData, messageOf } from '../../services/adminService';
import { Notice, PageHeading, Pagination } from '../../components/PageTools';

interface Workflow { id: string; objective: string; workflowType: string; status: string; createdAt: string; decisionReason: string }
interface Detail extends Workflow { sessionId?: string; zoneId?: string; stepResultsJson: string; planJson: string; errorLog?: string; updatedAt: string }
function readableJson(value: string) { try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; } }
export function EnforcementPage() {
  const { settings } = useContext(SettingsContext);
  const [status, setStatus] = useState('AwaitingApproval');
  const list = useAdminPage<Workflow>(`/api/admin/workflows${status ? `?status=${status}` : ''}`);
  const [detail, setDetail] = useState<Detail | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const scan = async () => {
    setBusy(true); setError(''); setMessage('');
    try { const result = await postData<{ triggeredCount: number }>('/api/agent/workflows/scan', {}); setMessage(`Overstay check completed. ${result.triggeredCount} workflows triggered.`); list.reload(); }
    catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  const retry = async () => {
    if (!detail) return;
    setBusy(true); setError('');
    try { const result = await postData<Detail>(`/api/agent/workflows/${detail.id}/retry`, {}); setDetail(result); setMessage(`Retry status: ${result.status}.`); list.reload(); }
    catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  const select = async (id: string) => {
    setBusy(true); setError('');
    try { setDetail(await getData<Detail>(`/api/agent/workflows/${id}`)); setReason(''); }
    catch (err) { setError(messageOf(err)); } finally { setBusy(false); }
  };
  const decide = async (action: 'approve' | 'reject') => {
    if (!detail) return;
    if (action === 'reject' && reason.trim().length < 5) { setError('Enter a rejection reason of at least 5 characters.'); return; }
    if (!window.confirm(`${action === 'approve' ? 'Approve' : 'Reject'} this ${detail.workflowType} proposal?`)) return;
    setBusy(true); setError(''); setMessage('');
    try { const updated = await postData<Detail>(`/api/agent/workflows/${detail.id}/${action}`, action === 'reject' ? { reason: reason.trim() } : {}); setDetail(updated); setMessage(`Decision recorded. Workflow status: ${updated.status}.`); list.reload(); }
    catch (err) { setError(messageOf(err)); list.reload(); } finally { setBusy(false); }
  };
  return <><PageHeading title="AI Enforcement" description="Review proposed actions and inspect execution history."><button className="btn btn-primary" disabled={busy} onClick={() => void scan()}>Check overstays</button><select aria-label="Workflow status" value={status} onChange={e => { setStatus(e.target.value); list.setPage(1); setDetail(null); }}><option value="">All statuses</option>{['AwaitingApproval', 'Running', 'Approved', 'Rejected', 'Failed'].map(s => <option key={s}>{s}</option>)}</select><button className="btn btn-secondary" onClick={list.reload}>Refresh</button></PageHeading><Notice error={error || list.error} message={message} />
    <div className="reservation-grid"><section className="glass-panel admin-panel"><h2>Workflow queue</h2>{list.loading ? <p role="status">Loading workflowsâ€¦</p> : !list.error && <>{list.data?.items.map(w => <button className={`workflow-row ${detail?.id === w.id ? 'selected' : ''}`} key={w.id} disabled={busy} onClick={() => void select(w.id)}><strong>{w.objective}</strong><small>{w.workflowType} Â· {w.status}</small><small>{new Date(w.createdAt).toLocaleString()}</small></button>)}{list.data?.items.length === 0 && <p>No workflows match this status.</p>}</>}<Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} /></section>
    <section className="glass-panel admin-panel"><h2>Proposal details</h2>{detail ? <><p><strong>{detail.objective}</strong></p><p>Type: {detail.workflowType} Â· Status: {detail.status}</p><p>Created: {new Date(detail.createdAt).toLocaleString()}</p><p>Session: {detail.sessionId || 'Not linked'}</p><p>Zone: {detail.zoneId || 'Not linked'}</p><p>System currency: {settings.defaultCurrency}</p><h3>Agent evidence and proposed action</h3><pre className="proposal-json">{readableJson(detail.stepResultsJson)}</pre><details><summary>Execution plan</summary><pre className="proposal-json">{readableJson(detail.planJson)}</pre></details>{detail.decisionReason && <p>Decision: {detail.decisionReason}</p>}{detail.errorLog && <Notice error={detail.errorLog} />}{detail.status === 'Failed' && <button className="btn btn-secondary" disabled={busy} onClick={() => void retry()}>Retry workflow</button>}{detail.status === 'AwaitingApproval' && <><label>Rejection reason<textarea maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label><div className="admin-actions"><button className="btn btn-primary" disabled={busy} onClick={() => void decide('approve')}>Approve action</button><button className="btn btn-secondary" disabled={busy || reason.trim().length < 5} onClick={() => void decide('reject')}>Reject action</button></div></>}</> : <p>Select a workflow to inspect its proposal and evidence.</p>}</section></div>
  </>;
}

