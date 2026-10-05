import type { ReactNode } from 'react';

export function PageHeading({ title, description, children }: { title: string; description: string; children?: ReactNode }) {
  return <div className="dashboard-heading"><div><span className="eyebrow">ADMIN WORKSPACE</span><h1>{title}</h1><p>{description}</p></div><div className="admin-actions">{children}</div></div>;
}
export function Notice({ error, message }: { error?: string; message?: string }) {
  return <>{error && <div className="admin-notice error" role="alert">{error}</div>}{message && <div className="admin-notice" role="status">{message}</div>}</>;
}
export function Pagination({ page, total, onPage }: { page: number; total: number; onPage: (page: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / 20));
  return <div className="admin-pagination"><span>{total} records · Page {page} of {pages}</span><button className="btn btn-secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</button><button className="btn btn-secondary" disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</button></div>;
}
