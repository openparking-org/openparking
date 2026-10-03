import { apiGet, apiPost } from '../../lib/apiClient';
import type { PagedResponse } from '../../hooks/usePaginatedQuery';

export type BookingStatus =
  | 'Pending' | 'Confirmed' | 'Active' | 'Completed' | 'Cancelled' | 'Expired';

export interface Booking {
  id: string;
  userId: string;
  slotId: string;
  slotNumber: string;
  zoneName: string;
  startTime: string;
  endTime: string;
  status: BookingStatus;
  qrCodeContent: string;
  estimatedFee: number;
  createdAt: string;
}

export interface FeeBreakdown {
  billableHours: number;
  baseHourlyRate: number;
  appliedSurgeMultiplier: number;
  effectiveHourlyRate: number;
  surgeWasCapped: boolean;
  amount: number;
}

export interface BookingRequest {
  slotId: string;
  /** ISO-8601 in UTC. The owner is taken from the bearer token, never sent. */
  startTime: string;
  endTime: string;
  surgeMultiplier?: number;
}

export function fetchBookings(page: number, pageSize: number): Promise<PagedResponse<Booking>> {
  return apiGet<PagedResponse<Booking>>('/api/bookings', { page, pageSize });
}

/** Prices a window without reserving it. */
export function quoteBooking(request: BookingRequest): Promise<FeeBreakdown> {
  return apiPost<FeeBreakdown>('/api/bookings/quote', request);
}

export function createBooking(request: BookingRequest): Promise<{ booking: Booking; fee: FeeBreakdown }> {
  return apiPost<{ booking: Booking; fee: FeeBreakdown }>('/api/bookings', request);
}
