import { useCallback, useEffect, useState } from 'react';
import { getData, messageOf, type Page } from '../services/adminService';

export function useAdminPage<T>(path: string) {
  const [data, setData] = useState<Page<T> | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  const reload = useCallback(() => setRevision(n => n + 1), []);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    const separator = path.includes('?') ? '&' : '?';
    getData<Page<T>>(`${path}${separator}page=${page}&pageSize=20&search=${encodeURIComponent(search)}`, controller.signal)
      .then(result => { if (!controller.signal.aborted) setData(result); })
      .catch(err => { if (!controller.signal.aborted) setError(messageOf(err)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [path, page, search, revision]);
  return { data, page, setPage, search, setSearch: (value: string) => { setPage(1); setSearch(value); }, error, loading, reload };
}
