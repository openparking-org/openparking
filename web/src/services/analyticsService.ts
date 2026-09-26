// API Service for OpenParking Admin Analytics Dashboard (GitHub Issue #26)
// design.md §21.2 & §8.3

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

export interface AnalyticsSummary {
  totalSessions: number;
  activeSessions: number;
  completedSessions: number;
  overstaySessions: number;
  totalRevenue: number;
  totalPenalties: number;
  avgDurationMinutes: number;
  pendingWorkflowsCount: number;
  totalWorkflowsCount: number;
  from: string;
  to: string;
}

export interface DailyRevenue {
  date: string;
  revenue: number;
}

export interface ZoneOccupancy {
  zoneId: string;
  zoneName: string;
  zoneCode: string;
  totalCapacity: number;
  occupiedSlots: number;
  availableSlots: number;
  reservedSlots: number;
  occupancyPercentage: number;
}

export interface WeeklyViolation {
  week: string;
  weekLabel: string;
  count: number;
}

export interface WorkflowsSummary {
  pending: number;
  approved: number;
  autoApproved: number;
  rejected: number;
  failed: number;
  running: number;
  total: number;
}

export interface WorkflowProposal {
  id: string;
  workflowType: string;
  objective: string;
  status: string;
  requiresApproval: boolean;
  sessionId?: string;
  zoneId?: string;
  triggeredAt: string;
  planJson?: string;
  stepResultsJson?: string;
  executionSummaryJson?: string;
}

const BASE_URL = (import.meta.env.VITE_API_URL || 'http://localhost:5000').replace(/\/+$/, '');

// In-memory fallback if localStorage is unavailable (e.g., node test environment)
let memoryToken: string | null = null;

export function getStoredToken(): string | null {
  if (typeof window !== 'undefined' && window.localStorage) {
    return (
      window.localStorage.getItem('openparking_token') ||
      window.localStorage.getItem('token') ||
      window.localStorage.getItem('adminToken') ||
      window.sessionStorage?.getItem('token') ||
      null
    );
  }
  return memoryToken;
}

export function setStoredToken(token: string): void {
  if (typeof window !== 'undefined' && window.localStorage) {
    window.localStorage.setItem('openparking_token', token);
  } else {
    memoryToken = token;
  }
}

export function clearStoredToken(): void {
  if (typeof window !== 'undefined' && window.localStorage) {
    window.localStorage.removeItem('openparking_token');
    window.localStorage.removeItem('token');
    window.localStorage.removeItem('adminToken');
  }
  memoryToken = null;
}

async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const url = `${BASE_URL}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`;
  const token = getStoredToken();

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string> || {}),
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  let response: Response;
  try {
    response = await fetch(url, {
      ...options,
      headers,
    });
  } catch (err: any) {
    throw new ApiError(0, `Network error connecting to OpenParking backend: ${err?.message || 'Server unreachable'}`);
  }

  if (!response.ok) {
    if (response.status === 401) {
      throw new ApiError(401, 'Unauthorized: Admin authentication token required.');
    }
    if (response.status === 403) {
      throw new ApiError(403, 'Forbidden: ParkingAdmin or SystemAdmin role required.');
    }

    let errorDetail = response.statusText;
    try {
      const errJson = await response.json();
      errorDetail = errJson.message || errJson.title || errJson.error || JSON.stringify(errJson);
    } catch {
      // ignore json parse error
    }
    throw new ApiError(response.status, `API request failed with status ${response.status}: ${errorDetail}`);
  }

  // Handle empty 204 or empty bodies
  if (response.status === 204) {
    return {} as T;
  }

  try {
    return (await response.json()) as T;
  } catch {
    return {} as T;
  }
}

export const analyticsService = {
  async getSummary(days: number = 30, from?: string, to?: string): Promise<AnalyticsSummary> {
    const params = new URLSearchParams();
    if (days) params.append('days', days.toString());
    if (from) params.append('from', from);
    if (to) params.append('to', to);

    const query = params.toString() ? `?${params.toString()}` : '';
    return request<AnalyticsSummary>(`/api/analytics/summary${query}`);
  },

  async getDailyRevenue(days: number = 30, from?: string, to?: string): Promise<DailyRevenue[]> {
    const params = new URLSearchParams();
    if (days) params.append('days', days.toString());
    if (from) params.append('from', from);
    if (to) params.append('to', to);

    const query = params.toString() ? `?${params.toString()}` : '';
    return request<DailyRevenue[]>(`/api/analytics/revenue/daily${query}`);
  },

  async getOccupancy(): Promise<ZoneOccupancy[]> {
    return request<ZoneOccupancy[]>('/api/analytics/occupancy');
  },

  async getWeeklyViolations(weeks: number = 8, from?: string, to?: string): Promise<WeeklyViolation[]> {
    const params = new URLSearchParams();
    if (weeks) params.append('weeks', weeks.toString());
    if (from) params.append('from', from);
    if (to) params.append('to', to);

    const query = params.toString() ? `?${params.toString()}` : '';
    return request<WeeklyViolation[]>(`/api/analytics/violations/weekly${query}`);
  },

  async getWorkflowsSummary(from?: string, to?: string): Promise<WorkflowsSummary> {
    const params = new URLSearchParams();
    if (from) params.append('from', from);
    if (to) params.append('to', to);

    const query = params.toString() ? `?${params.toString()}` : '';
    return request<WorkflowsSummary>(`/api/analytics/workflows/summary${query}`);
  },

  async getPendingWorkflows(): Promise<WorkflowProposal[]> {
    return request<WorkflowProposal[]>('/api/agent/workflows/pending');
  },

  async approveWorkflow(id: string, reason: string = 'Approved via Admin Analytics Dashboard'): Promise<any> {
    return request<any>(`/api/agent/workflows/${id}/approve`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  },

  async rejectWorkflow(id: string, reason: string = 'Rejected via Admin Analytics Dashboard'): Promise<any> {
    return request<any>(`/api/agent/workflows/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  }
};
