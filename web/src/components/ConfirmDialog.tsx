import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { AlertTriangle, HelpCircle } from 'lucide-react';

export interface ConfirmOptions {
  title: string;
  body?: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

type Confirm = (options: ConfirmOptions) => Promise<boolean>;
const ConfirmContext = createContext<Confirm>(async options => window.confirm(options.title));

/** Promise-based replacement for window.confirm that matches the admin theme. */
// eslint-disable-next-line react-refresh/only-export-components
export function useConfirm() { return useContext(ConfirmContext); }

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [request, setRequest] = useState<(ConfirmOptions & { resolve: (value: boolean) => void }) | null>(null);
  const confirmButton = useRef<HTMLButtonElement>(null);
  const returnFocus = useRef<HTMLElement | null>(null);
  const confirm = useCallback<Confirm>(options => new Promise(resolve => {
    returnFocus.current = document.activeElement as HTMLElement | null;
    setRequest({ ...options, resolve });
  }), []);
  const close = useCallback((value: boolean) => {
    setRequest(current => { current?.resolve(value); return null; });
    returnFocus.current?.focus?.();
  }, []);
  useEffect(() => {
    if (!request) return;
    confirmButton.current?.focus();
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') close(false); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [request, close]);
  return <ConfirmContext.Provider value={confirm}>
    {children}
    {request && <div className="dialog-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) close(false); }}>
      <div className="dialog" role="alertdialog" aria-modal="true" aria-labelledby="confirm-title" aria-describedby={request.body ? 'confirm-body' : undefined}>
        <span className={`dialog-icon ${request.danger ? 'tone-crit' : ''}`}>{request.danger ? <AlertTriangle size={20} aria-hidden="true" /> : <HelpCircle size={20} aria-hidden="true" />}</span>
        <h2 id="confirm-title">{request.title}</h2>
        {request.body && <p id="confirm-body">{request.body}</p>}
        <div className="dialog-actions">
          <button type="button" className="btn btn-secondary" onClick={() => close(false)}>{request.cancelLabel || 'Cancel'}</button>
          <button type="button" ref={confirmButton} className={`btn ${request.danger ? 'btn-danger-solid' : 'btn-primary'}`} onClick={() => close(true)}>{request.confirmLabel || 'Confirm'}</button>
        </div>
      </div>
    </div>}
  </ConfirmContext.Provider>;
}
