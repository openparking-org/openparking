import { afterEach, describe, it, expect, vi } from 'vitest';
import { analyticsService, ApiError, getStoredToken, setStoredToken, clearStoredToken } from '../services/analyticsService';
import { useAuthStore } from '../store/authStore';

describe('Analytics Service Tests', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    useAuthStore.setState({ token: null });
    clearStoredToken();
  });

  it('uses the signed-in session and unwraps the backend analytics response', async () => {
    useAuthStore.setState({ token: 'signed-in-token' });
    const summary = { totalSessions: 12, totalRevenue: 420 };
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ success: true, data: summary })));
    vi.stubGlobal('fetch', fetchMock);
    await expect(analyticsService.getSummary(7)).resolves.toEqual(summary);
    expect(fetchMock.mock.calls[0][0]).toContain('/api/analytics/summary?days=7');
    expect(new Headers(fetchMock.mock.calls[0][1].headers).get('Authorization')).toBe('Bearer signed-in-token');
  });

  it('adapts backend workflow timestamps for the approval queue', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      success: true, data: [{ id: 'workflow-1', createdAt: '2026-10-04T10:00:00Z' }]
    }))));
    const proposals = await analyticsService.getPendingWorkflows();
    expect(proposals[0].triggeredAt).toBe('2026-10-04T10:00:00Z');
  });

  it('shows structured backend errors without swallowing failed envelopes', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      success: false, error: { message: 'Analytics unavailable' }
    }))));
    await expect(analyticsService.getSummary()).rejects.toThrow('Analytics unavailable');
  });
  it('stores and clears auth tokens correctly', () => {
    clearStoredToken();
    expect(getStoredToken()).toBeNull();

    setStoredToken('test-admin-jwt-token');
    expect(getStoredToken()).toBe('test-admin-jwt-token');

    clearStoredToken();
    expect(getStoredToken()).toBeNull();
  });

  it('ApiError constructs with status and message', () => {
    const error = new ApiError(401, 'Unauthorized access');
    expect(error.status).toBe(401);
    expect(error.message).toBe('Unauthorized access');
    expect(error.name).toBe('ApiError');
  });

  it('analyticsService exposes all required design.md §21 methods', () => {
    expect(typeof analyticsService.getSummary).toBe('function');
    expect(typeof analyticsService.getDailyRevenue).toBe('function');
    expect(typeof analyticsService.getOccupancy).toBe('function');
    expect(typeof analyticsService.getWeeklyViolations).toBe('function');
    expect(typeof analyticsService.getWorkflowsSummary).toBe('function');
    expect(typeof analyticsService.getPendingWorkflows).toBe('function');
    expect(typeof analyticsService.approveWorkflow).toBe('function');
    expect(typeof analyticsService.rejectWorkflow).toBe('function');
  });
});
