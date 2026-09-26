import { describe, it, expect } from 'vitest';
import { analyticsService, ApiError, getStoredToken, setStoredToken, clearStoredToken } from '../services/analyticsService';

describe('Analytics Service Tests', () => {
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
