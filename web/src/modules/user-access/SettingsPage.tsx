import { useCallback, useContext, useEffect, useState } from 'react';
import { SettingsContext } from '../../App';
import { apiClient } from '../../lib/apiClient';
import { getData, messageOf, type Setting } from '../../services/adminService';
import { Notice, PageHeading } from '../../components/PageTools';

export function SettingsPage() {
  const { updateSettings } = useContext(SettingsContext);
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
      const aliases = ['overstay.grace_period_minutes', 'overstay.penalty_per_extra_hour'];
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
  return <><PageHeading title="System Settings" description="Persisted pricing, overstay, and review policies."><button className="btn btn-secondary" disabled={loading || !!saving} onClick={() => void load()}>Reload</button></PageHeading><Notice error={error} message={message} />
    {loading ? <p role="status">Loading settings…</p> : Object.entries(settings).map(([category, items]) => <section className="glass-panel admin-panel" key={category}><h2>{category}</h2>{items.map(item => <form className="setting-row" key={item.key} onSubmit={event => { event.preventDefault(); void save(item); }}><div><strong>{item.description || item.key}</strong><small>{item.key}</small></div><label><span className="sr-only">{item.description || item.key}</span>{item.key === 'pricing.default_currency' ? <select value={draft[item.key]} onChange={e => setDraft({ ...draft, [item.key]: e.target.value })}>{['USD', 'LKR', 'EUR', 'GBP'].map(code => <option key={code}>{code}</option>)}</select> : <input required aria-label={item.key} value={draft[item.key]} onChange={e => setDraft({ ...draft, [item.key]: e.target.value })} />}</label><button className="btn btn-primary" disabled={!!saving || draft[item.key] === item.value}>{saving === item.key ? 'Saving…' : 'Save'}</button></form>)}</section>)}
  </>;
}
