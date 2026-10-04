import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { apiClient, ApiError } from '../lib/apiClient';

const auth = vi.hoisted(() => ({ token: 'stale-token', logout: vi.fn() }));
vi.mock('../store/authStore', () => ({ useAuthStore: { getState: () => auth } }));
vi.mock('../config', () => ({ config: { apiUrl: 'http://localhost:5000' } }));

describe('API login and authentication errors', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal('window', { location: { href: '/login', pathname: '/login' } });
  });
  afterEach(() => vi.unstubAllGlobals());

  it('keeps failed login on the page and displays the API error', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(
      JSON.stringify({ error: { message: 'Invalid email or password.' } }), { status: 401 }
    )));
    await expect(apiClient.post('/api/users/login', {}))
      .rejects.toMatchObject({ status: 401, message: 'Invalid email or password.' });
    expect(auth.logout).not.toHaveBeenCalled();
    expect(window.location.href).toBe('/login');
  });

  it('does not send a stale bearer token with a new login', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('{}'));
    vi.stubGlobal('fetch', fetchMock);
    await apiClient.post('/api/users/login', {});
    const options = fetchMock.mock.calls[0][1] as RequestInit;
    expect(new Headers(options.headers).has('Authorization')).toBe(false);
  });

  it('clears an expired session and redirects protected requests to login', async () => {
    vi.stubGlobal('window', { location: { href: '/', pathname: '/' } });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status: 401 })));
    await expect(apiClient.get('/api/users/me')).rejects.toBeInstanceOf(ApiError);
    expect(auth.logout).toHaveBeenCalledOnce();
    expect(window.location.href).toBe('/login');
  });

  it('shows a connection error when the backend is unreachable', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));
    await expect(apiClient.post('/api/users/login', {}))
      .rejects.toMatchObject({ status: 0, message: 'Unable to connect to the server. Please try again shortly.' });
  });
});
