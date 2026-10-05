import { apiClient } from '../lib/apiClient';

export interface Page<T> { items: T[]; totalCount: number; page: number; pageSize: number }
export interface Zone { id: string; name: string; code: string; latitude: number; longitude: number; baseHourlyRate: number; totalCapacity: number; availableCount: number; occupiedCount: number; reservedCount: number; mappedCount: number; currency: string }
export interface Driver { id: string; fullName: string; email: string; hasDisabilityPermit: boolean }
export interface Slot { id: string; slotNumber: string; type: string; status: string; floor: number; boundingBoxJson?: string; canvasX?: number; canvasY?: number; canvasWidth?: number; canvasHeight?: number; assignedSensorId?: string; assignedCameraId?: string; nearestWaypointId?: string }
export interface FloorPlan { id: string; floorName: string; floorOrder: number; imageUrl: string; imageWidthPx: number; imageHeightPx: number; waypointGraphJson: string }
export interface Booking { id: string; userId: string; driverName: string; driverEmail: string; vehiclePlate: string; zoneName: string; slotNumber: string; startTime: string; endTime: string; status: string; estimatedFee: number; currency: string; isPaid: boolean; session?: { id: string; status: string; checkInTime: string; checkOutTime?: string; totalFee: number; penaltyFee: number } }
export interface Setting { key: string; value: string; description: string; category: string; updatedAt: string }
export const messageOf = (error: unknown) => error instanceof Error ? error.message : 'Something went wrong. Please retry.';
export async function getData<T>(endpoint: string, signal?: AbortSignal): Promise<T> { return (await apiClient.get(endpoint, { signal })).data; }
export async function postData<T>(endpoint: string, body: unknown): Promise<T> { return (await apiClient.post(endpoint, body)).data; }
export async function allZones(signal?: AbortSignal): Promise<Zone[]> {
  const zones: Zone[] = [];
  let page = 1;
  for (;;) {
    const result = await getData<Page<Zone>>(`/api/admin/zones?page=${page}&pageSize=100`, signal);
    zones.push(...result.items);
    if (zones.length >= result.totalCount || result.items.length === 0) return zones;
    page++;
  }
}
