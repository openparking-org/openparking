import { useState, useEffect, useCallback } from 'react';

export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export function usePaginatedQuery<T>(
  fetchFn: (page: number, pageSize: number, search?: string) => Promise<PagedResponse<T>>,
  initialPage = 1,
  pageSize = 10
) {
  const [data, setData] = useState<PagedResponse<T> | null>(null);
  const [page, setPage] = useState(initialPage);
  const [search, setSearch] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  const loadData = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await fetchFn(page, pageSize, search);
      setData(result);
    } catch (err: any) {
      setError(err);
    } finally {
      setIsLoading(false);
    }
  }, [fetchFn, page, pageSize, search]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  return {
    items: data?.items ?? [],
    totalCount: data?.totalCount ?? 0,
    totalPages: data?.totalPages ?? 1,
    page,
    setPage,
    search,
    setSearch,
    isLoading,
    error,
    reload: loadData,
  };
}
