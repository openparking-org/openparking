import { useContext, useState } from 'react';
import { SettingsContext } from '../../App';
import { useAdminPage } from '../../hooks/useAdminPage';
import { getData, postData, messageOf } from '../../services/adminService';
import { EmptyState, Loading, Notice, PageHeading, Pagination, StatusBadge } from '../../components/PageTools';
import { humanize } from '../../lib/status';
import { useConfirm } from '../../components/ConfirmDialog';
import { Bot, Check, MousePointerClick, RefreshCw, RotateCcw, ScanSearch, X } from 'lucide-react';

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
  const confirm = useConfirm();
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
    if (!await confirm({ title: `${action === 'approve' ? 'Approve' : 'Reject'} this ${humanize(detail.workflowType).toLowerCase()} proposal?`, body: action === 'approve' ? 'The agent will carry out the proposed action. This is recorded in the audit log.' : `Reason: “${reason.trim()}”`, confirmLabel: action === 'approve' ? 'Approve action' : 'Reject action', danger: action === 'reject' })) return;
    setBusy(true); setError(''); setMessage('');
    try { const updated = await postData<Detail>(`/api/agent/workflows/${detail.id}/${action}`, action === 'reject' ? { reason: reason.trim() } : {}); setDetail(updated); setMessage(`Decision recorded. Workflow status: ${updated.status}.`); list.reload(); }
    catch (err) { setError(messageOf(err)); list.reload(); } finally { setBusy(false); }
  };
  return <>
    <PageHeading title="AI Enforcement" description="Review agent proposals before they take effect and inspect execution history.">
      <button className="btn btn-secondary" onClick={list.reload}><RefreshCw size={16} aria-hidden="true" />Refresh</button>
      <button className="btn btn-primary" disabled={busy} onClick={() => void scan()}><ScanSearch size={16} aria-hidden="true" />Check overstays</button>
    </PageHeading>
    <Notice error={error || list.error} message={message} />
    <div className="segmented" role="group" aria-label="Filter by status" style={{ marginBottom: 16 }}>{['AwaitingApproval', 'Running', 'Approved', 'Rejected', 'Failed', ''].map(s => <button key={s || 'all'} type="button" aria-pressed={status === s} onClick={() => { setStatus(s); list.setPage(1); setDetail(null); }}>{s ? humanize(s) : 'All'}</button>)}</div>
    <div className="grid-split grid-queue">
      <section className="card">
        <div className="card-header" style={{ paddingBottom: 14, borderBottom: '1px solid var(--line)' }}><div><h2>Workflow queue</h2><p>{list.data?.totalCount ?? 0} {status ? humanize(status).toLowerCase() : 'total'}</p></div></div>
        {list.loading ? <Loading label="Loading workflows…" /> : !list.error && (list.data?.items.length === 0
          ? <EmptyState icon={Bot} title="Nothing in this queue">{status === 'AwaitingApproval' ? 'No proposals need your decision. Run “Check overstays” to scan active sessions.' : 'No workflows match this status.'}</EmptyState>
          : <div className="queue">{list.data?.items.map(w => <button type="button" className="queue-item" aria-current={detail?.id === w.id} key={w.id} disabled={busy} onClick={() => void select(w.id)}>
            <strong>{w.objective}</strong>
            <span className="row"><StatusBadge status={w.status} /><span>{humanize(w.workflowType)}</span><span>·</span><span>{new Date(w.createdAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })}</span></span>
          </button>)}</div>)}
        <Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} />
      </section>
      <section className="card">
        {detail ? <>
          <div className="card-header"><div><h2>{detail.objective}</h2><p>{humanize(detail.workflowType)} workflow</p></div><StatusBadge status={detail.status} /></div>
          <div className="card-body">
            <dl className="kv">
              <dt>Created</dt><dd>{new Date(detail.createdAt).toLocaleString()}</dd>
              <dt>Session</dt><dd className="mono">{detail.sessionId || 'Not linked'}</dd>
              <dt>Zone</dt><dd className="mono">{detail.zoneId || 'Not linked'}</dd>
              <dt>Currency</dt><dd>{settings.defaultCurrency}</dd>
              {detail.decisionReason && <><dt>Decision</dt><dd>{detail.decisionReason}</dd></>}
            </dl>
            <hr className="divider" />
            <h3 style={{ marginBottom: 10 }}>Agent evidence and proposed action</h3>
            <pre className="code-block">{readableJson(detail.stepResultsJson)}</pre>
            <details className="disclosure" style={{ marginTop: 14 }}><summary>Execution plan</summary><div><pre className="code-block">{readableJson(detail.planJson)}</pre></div></details>
            {detail.errorLog && <div style={{ marginTop: 14 }}><Notice error={detail.errorLog} /></div>}
            {detail.status === 'AwaitingApproval' && <label className="field" style={{ marginTop: 14 }}><span className="field-label">Rejection reason</span><textarea maxLength={1000} placeholder="Required to reject (at least 5 characters)" value={reason} onChange={e => setReason(e.target.value)} /></label>}
          </div>
          {detail.status === 'Failed' && <div className="card-footer"><button className="btn btn-secondary" disabled={busy} onClick={() => void retry()}><RotateCcw size={16} aria-hidden="true" />Retry workflow</button></div>}
          {detail.status === 'AwaitingApproval' && <div className="card-footer">
            <span className="subtle">Human approval is required before this action runs.</span>
            <div className="row" style={{ marginLeft: 'auto' }}>
              <button className="btn btn-danger" disabled={busy || reason.trim().length < 5} onClick={() => void decide('reject')}><X size={16} aria-hidden="true" />Reject</button>
              <button className="btn btn-primary" disabled={busy} onClick={() => void decide('approve')}><Check size={16} aria-hidden="true" />Approve action</button>
            </div>
          </div>}
        </> : <EmptyState icon={MousePointerClick} title="Select a workflow">Choose an item from the queue to inspect the agent’s evidence and proposed action.</EmptyState>}
      </section>
    </div>
  </>;
}
