import { useState } from 'react';
import { apiClient } from '../../lib/apiClient';
import { useAdminPage } from '../../hooks/useAdminPage';
import { postData, messageOf } from '../../services/adminService';
import { EmptyState, Loading, Notice, PageHeading, Pagination, StatusBadge } from '../../components/PageTools';
import { Check, ExternalLink, FileCheck, FileX, RefreshCw, ScanText, X } from 'lucide-react';

interface Validation {
  valid: boolean;
  document_status: 'read' | 'unavailable';
  reason?: string;
  issues?: string[];
  extracted_fields?: { permit_number?: string | null; expiry_date?: string | null; jurisdiction?: string | null; readability?: string };
}
interface Permit {
  id: string; fullName: string; permitNumber: string; jurisdiction: string;
  expiryDate: string; documentImageUrl: string; status: string; createdAt: string;
  reviewedAt?: string; reviewNotes?: string; rejectionReason?: string; aiValidation?: Validation | null;
}
function documentUrl(value: string): string | undefined {
  try { const url = new URL(value); return ['http:', 'https:'].includes(url.protocol) ? value : undefined; }
  catch { return undefined; }
}
function isUploadedImage(value: string): boolean {
  return /^data:image\/(png|jpeg);base64,[A-Za-z0-9+/=\r\n]+$/.test(value);
}

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
    try {
      const result = await postData<Validation>(`/api/admin/permits/${permit.id}/validation`, {});
      setResults(current => ({ ...current, [permit.id]: result }));
    } catch (err) { setError(messageOf(err)); }
    finally { setBusy(''); }
  };
  const review = async (permit: Permit, decision: 'Verified' | 'Rejected') => {
    const reason = notes[permit.id]?.trim() || '';
    if (decision === 'Rejected' && !reason) { setError('Enter a rejection reason.'); return; }
    if (decision === 'Verified' && new Date(permit.expiryDate).getTime() <= Date.now()) { setError('An expired permit cannot be verified.'); return; }
    setBusy(permit.id); setError(''); setMessage('');
    try {
      await apiClient.patch(`/api/users/permits/${permit.id}/review`, { decision, notes: reason || 'Manually verified by admin' });
      setMessage(decision === 'Verified' ? 'Permit verified.' : 'Permit rejected.'); list.reload();
    } catch (err) { setError(messageOf(err)); list.reload(); }
    finally { setBusy(''); }
  };

  const date = (value: string) => new Date(value).toLocaleDateString([], { dateStyle: 'medium' });
  return <>
    <PageHeading title="Disability Permit Review" description="Compare the uploaded document with its details, then record your decision.">
      <button className="btn btn-secondary" onClick={list.reload}><RefreshCw size={16} aria-hidden="true" />Refresh</button>
    </PageHeading>
    <div className="segmented" role="group" aria-label="Filter by status" style={{ marginBottom: 16 }}>{['Pending', 'Verified', 'Rejected', 'Expired', ''].map(s => <button key={s || 'all'} type="button" aria-pressed={filter === s} onClick={() => { setFilter(s); list.setPage(1); }}>{s || 'All'}</button>)}</div>
    <Notice error={error || list.error} message={message} />
    {list.loading ? <Loading label="Loading permits…" /> : !list.error && <>
      {list.data?.items.map(permit => {
        const validation = results[permit.id] || permit.aiValidation;
        const fields = validation?.extracted_fields;
        const url = documentUrl(permit.documentImageUrl);
        const expired = new Date(permit.expiryDate).getTime() <= Date.now();
        const readingTone = validation?.document_status === 'unavailable' ? 'warn' : validation?.valid ? 'good' : 'crit';
        return <article className="card" key={permit.id}>
          <div className="card-header"><div><h2>{permit.fullName}</h2><p>Submitted {new Date(permit.createdAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })}</p></div><StatusBadge status={permit.status} label={permit.status === 'Pending' ? 'Pending review' : undefined} /></div>
          <div className="card-body grid-2">
            <div className="stack">
              <dl className="kv">
                <dt>Permit number</dt><dd className="mono">{permit.permitNumber}</dd>
                <dt>Jurisdiction</dt><dd>{permit.jurisdiction}</dd>
                <dt>Expiry</dt><dd>{date(permit.expiryDate)} {expired && <span className="badge tone-crit" style={{ marginLeft: 6 }}>Expired</span>}</dd>
                {permit.reviewedAt && <><dt>Reviewed</dt><dd>{new Date(permit.reviewedAt).toLocaleString()}</dd></>}
                {permit.reviewNotes && <><dt>Notes</dt><dd>{permit.reviewNotes}</dd></>}
                {permit.rejectionReason && <><dt>Rejection</dt><dd>{permit.rejectionReason}</dd></>}
              </dl>
              {validation && <div className="reading" aria-live="polite">
                <div className="row-between"><strong className="row" style={{ gap: 8 }}><ScanText size={16} aria-hidden="true" />AI document reading</strong><span className={`badge tone-${readingTone}`}>{validation.document_status === 'unavailable' ? 'Unavailable' : validation.valid ? 'Fields match' : 'Needs attention'}</span></div>
                {fields && <dl className="kv">
                  <dt>Permit number</dt><dd>{fields.permit_number || 'Unreadable / missing'}</dd>
                  <dt>Authority</dt><dd>{fields.jurisdiction || 'Unreadable / missing'}</dd>
                  <dt>Expiry date</dt><dd>{fields.expiry_date || 'Unreadable / missing'}</dd>
                  <dt>Readability</dt><dd>{fields.readability || 'Unknown'}</dd>
                </dl>}
                {validation.reason && <p className="muted">{validation.reason}</p>}
                <p className="subtle" style={{ marginTop: 8 }}>Reading the document does not prove authenticity. An administrator must approve.</p>
              </div>}
            </div>
            <div className="permit-doc">
              {isUploadedImage(permit.documentImageUrl)
                ? <img src={permit.documentImageUrl} alt={`Uploaded permit for ${permit.fullName}`} />
                : url ? <a className="btn btn-secondary" href={url} target="_blank" rel="noopener noreferrer"><ExternalLink size={16} aria-hidden="true" />Open permit document</a>
                  : <EmptyState icon={FileX} title="Document unavailable">The file is missing or in an unsupported format.</EmptyState>}
            </div>
          </div>
          {permit.status === 'Pending' && <>
            <div style={{ padding: '0 20px 18px' }}><label className="field"><span className="field-label">Review notes</span><textarea maxLength={1000} placeholder="Optional for approval. Required to reject." value={notes[permit.id] || ''} onChange={e => setNotes(current => ({ ...current, [permit.id]: e.target.value }))} /></label></div>
            <div className="card-footer">
              <button className="btn btn-secondary" disabled={!!busy || validation?.document_status === 'read'} onClick={() => void validate(permit)}><ScanText size={16} aria-hidden="true" />
                {validation?.document_status === 'read' ? 'Document read' : busy === permit.id ? 'Reading…' : 'Read document with AI'}
              </button>
              <div className="row" style={{ marginLeft: 'auto' }}>
                <button className="btn btn-danger" disabled={!!busy || !notes[permit.id]?.trim()} onClick={() => void review(permit, 'Rejected')}><X size={16} aria-hidden="true" />Reject</button>
                <button className="btn btn-primary" disabled={!!busy || expired} title={expired ? 'Expired permits cannot be verified' : undefined} onClick={() => void review(permit, 'Verified')}><Check size={16} aria-hidden="true" />Verify permit</button>
              </div>
            </div>
          </>}
        </article>;
      })}
      {list.data?.items.length === 0 && <div className="card"><EmptyState icon={FileCheck} title={filter === 'Pending' ? 'All caught up' : 'No permits found'}>{filter === 'Pending' ? 'There are no permits waiting for review.' : 'No permits match this status.'}</EmptyState></div>}
    </>}
    {(list.data?.totalCount || 0) > 20 && <div className="card" style={{ marginTop: 20 }}><Pagination page={list.page} total={list.data?.totalCount || 0} onPage={list.setPage} /></div>}
  </>;
}
