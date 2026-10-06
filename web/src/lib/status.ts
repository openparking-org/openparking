// Maps API status values to a display tone and a readable label.

export type Tone = 'good' | 'warn' | 'crit' | 'info' | 'neutral';

const tones: Record<string, Tone> = {
  // Bookings and sessions
  Pending: 'neutral', Confirmed: 'info', Active: 'good', Completed: 'neutral', Cancelled: 'neutral', Expired: 'warn', Overstay: 'crit', OverstayDetected: 'crit',
  // AI workflows
  AwaitingApproval: 'warn', Running: 'info', Approved: 'good', AutoApproved: 'good', Rejected: 'crit', Failed: 'crit',
  // Permits
  Verified: 'good',
  // Bays
  Available: 'good', Reserved: 'warn', Occupied: 'crit', Maintenance: 'neutral',
  // Device events
  Info: 'info', Warning: 'warn', Error: 'crit',
};

/** "AwaitingApproval" → "Awaiting approval" */
export function humanize(value: string) {
  const words = value.replace(/[_.]/g, ' ').replace(/([a-z0-9])([A-Z])/g, '$1 $2').trim().toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

export function toneOf(status: string): Tone { return tones[status] || 'neutral'; }
