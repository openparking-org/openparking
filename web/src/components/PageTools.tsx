import type { ReactNode } from 'react';
import { toneOf, humanize, type Tone } from '../lib/status';
import { AlertCircle, CheckCircle2, ChevronLeft, ChevronRight, Inbox, Loader2, type LucideIcon } from 'lucide-react';

export function PageHeading({ title, description, eyebrow, children }: { title: string; description: string; eyebrow?: string; children?: ReactNode }) {
  return <div className="page-header">
    <div>{eyebrow && <span className="eyebrow">{eyebrow}</span>}<h1>{title}</h1><p>{description}</p></div>
    {children && <div className="page-actions">{children}</div>}
  </div>;
}

export function Notice({ error, message }: { error?: string; message?: string }) {
  return <>
    {error && <div className="notice tone-crit" role="alert"><AlertCircle size={18} aria-hidden="true" /><div>{error}</div></div>}
    {message && <div className="notice tone-good" role="status"><CheckCircle2 size={18} aria-hidden="true" /><div>{message}</div></div>}
  </>;
}

export function Pagination({ page, total, onPage }: { page: number; total: number; onPage: (page: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / 20));
  if (total === 0) return null;
  return <div className="pagination">
    <span>{total} {total === 1 ? 'record' : 'records'} · Page {page} of {pages}</span>
    <button className="btn btn-secondary btn-sm" disabled={page <= 1} onClick={() => onPage(page - 1)}><ChevronLeft size={16} aria-hidden="true" />Previous</button>
    <button className="btn btn-secondary btn-sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>Next<ChevronRight size={16} aria-hidden="true" /></button>
  </div>;
}

export function Loading({ label }: { label: string }) {
  return <div className="loading" role="status"><Loader2 size={18} className="spin" aria-hidden="true" />{label}</div>;
}

export function EmptyState({ title, children, icon: Icon = Inbox, action }: { title: string; children?: ReactNode; icon?: LucideIcon; action?: ReactNode }) {
  return <div className="empty">
    <span className="empty-icon"><Icon size={20} aria-hidden="true" /></span>
    <strong>{title}</strong>
    {children && <p>{children}</p>}
    {action}
  </div>;
}

export function StatusBadge({ status, label, tone }: { status: string; label?: string; tone?: Tone }) {
  const resolved = tone || toneOf(status);
  return <span className={`badge ${resolved === 'neutral' ? '' : `tone-${resolved}`}`}>{label || humanize(status)}</span>;
}
