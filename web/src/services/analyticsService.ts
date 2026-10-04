// API Service for OpenParking Admin Analytics Dashboard (GitHub Issue #26)
// design.md Â§21.2 & Â§8.3
import { apiClient } from '../lib/apiClient';
export { ApiError } from '../lib/apiClient';
import { useAuthStore } from '../store/authStore';


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

export function getStoredToken(): string | null { return useAuthStore.getState().token; }
export function setStoredToken(token: string): void { useAuthStore.setState({ token }); }
export function clearStoredToken(): void { useAuthStore.getState().logout(); }
async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const payload = options.method === 'POST'
    ? await apiClient.post(endpoint, options.body ? JSON.parse(String(options.body)) : {}, options)
    : await apiClient.get(endpoint, options);
  return payload.data as T;
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
    const proposals = await request<(WorkflowProposal & { createdAt?: string })[]>('/api/agent/workflows/pending');
    return proposals.map(proposal => ({ ...proposal, triggeredAt: proposal.triggeredAt || proposal.createdAt || '' }));
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
