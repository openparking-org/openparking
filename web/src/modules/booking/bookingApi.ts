import { apiGet } from '../../lib/apiClient';
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

export function fetchBookings(page: number, pageSize: number): Promise<PagedResponse<Booking>> {
  return apiGet<PagedResponse<Booking>>('/api/bookings', { page, pageSize });
}
