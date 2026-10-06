import { useCallback, useContext, useEffect, useState } from 'react';
import { SettingsContext } from '../../App';
import { apiClient } from '../../lib/apiClient';
import { getData, messageOf, type Setting } from '../../services/adminService';
import { Loading, Notice, PageHeading } from '../../components/PageTools';
import { RotateCcw } from 'lucide-react';

function unitOf(key: string, currency: string) {
  if (key.includes('confidence')) return '0–1';
  if (key.includes('multiplier')) return '×';
  if (/minutes|_mins/.test(key)) return 'min';
  if (key.includes('hours')) return 'h';
  if (/amount|cap|fee|rate|penalty/.test(key)) return currency;
  return '';
}
const isBoolean = (value: string) => value === 'true' || value === 'false';

export function SettingsPage() {
  const { settings: shared, updateSettings } = useContext(SettingsContext);
  const [settings, setSettings] = useState<Record<string, Setting[]>>({});
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const load = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const data = await getData<Record<string, Setting[]>>('/api/settings');
      const aliases = ['overstay.grace_period_minutes', 'overstay.penalty_per_extra_hour', 'permits.auto_approve_confidence'];
      const visible = Object.fromEntries(Object.entries(data).map(([category, items]) => [category, items.filter(item => !aliases.includes(item.key))]));
      setSettings(visible); setDraft(Object.fromEntries(Object.values(visible).flat().map(item => [item.key, item.value])));
    } catch (err) { setError(messageOf(err)); } finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  const save = async (item: Setting) => {
    setSaving(item.key); setError(''); setMessage('');
    try {
      await apiClient.put(`/api/settings/${encodeURIComponent(item.key)}`, { value: draft[item.key] });
      setSettings(current => ({ ...current, [item.category]: current[item.category].map(row => row.key === item.key ? { ...row, value: draft[item.key] } : row) }));
      if (item.key === 'pricing.default_currency') updateSettings({ defaultCurrency: draft[item.key] });
      if (item.key === 'overstay.penalty_fixed_amount') updateSettings({ baseFine: Number(draft[item.key]) });
      if (item.key === 'permits.auto_approve_confidence') updateSettings({ aiTolerance: Number(draft[item.key]) * 100 });
      setMessage(`${item.description || item.key} saved.`);
    } catch (err) { setError(messageOf(err)); } finally { setSaving(''); }
  };
  const changed = Object.values(settings).flat().filter(item => draft[item.key] !== item.value).length;
  return <>
    <PageHeading title="System Settings" description="Pricing, overstay and review policies. Changes apply immediately after saving.">
      <button className="btn btn-secondary" disabled={loading || !!saving} onClick={() => void load()}><RotateCcw size={16} aria-hidden="true" />{changed ? `Discard ${changed} change${changed > 1 ? 's' : ''}` : 'Reload'}</button>
    </PageHeading>
    <Notice error={error} message={message} />
    {loading ? <Loading label="Loading settings…" /> : <div className="stack">{Object.entries(settings).filter(([, items]) => items.length > 0).map(([category, items]) => <section className="card" key={category}>
      <div className="card-header" style={{ paddingBottom: 14, borderBottom: '1px solid var(--line)' }}><div><h2>{category.charAt(0).toUpperCase() + category.slice(1)}</h2><p>{items.length} {items.length === 1 ? 'setting' : 'settings'}</p></div></div>
      {items.map(item => {
        const dirty = draft[item.key] !== item.value;
        const unit = unitOf(item.key, shared.defaultCurrency);
        return <form className={`setting-row ${dirty ? 'changed' : ''}`} key={item.key} onSubmit={event => { event.preventDefault(); void save(item); }}>
          <div><strong>{item.description || item.key}</strong><code>{item.key}</code></div>
          <div className="control">{item.key === 'pricing.default_currency'
            ? <select aria-label={item.description || item.key} value={draft[item.key]} onChange={e => setDraft({ ...draft, [item.key]: e.target.value })}>{['USD', 'LKR', 'EUR', 'GBP'].map(code => <option key={code}>{code}</option>)}</select>
            : isBoolean(item.value)
              ? <button type="button" role="switch" className="switch" aria-checked={draft[item.key] === 'true'} aria-label={item.description || item.key} onClick={() => setDraft({ ...draft, [item.key]: draft[item.key] === 'true' ? 'false' : 'true' })} />
              : <div className="input-unit" style={{ width: '100%' }}><input required inputMode="decimal" aria-label={item.description || item.key} value={draft[item.key]} onChange={e => setDraft({ ...draft, [item.key]: e.target.value })} />{unit && <span>{unit}</span>}</div>}
          </div>
          <button className={`btn btn-sm ${dirty ? 'btn-primary' : 'btn-ghost'}`} disabled={!!saving || !dirty}>{saving === item.key ? 'Saving…' : dirty ? 'Save' : 'Saved'}</button>
        </form>;
      })}
    </section>)}</div>}
  </>;
}
