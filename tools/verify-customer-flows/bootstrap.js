(async () => {
  localStorage.setItem('openparking-auth', JSON.stringify({ state: { token: 'verification-admin', user: {
    id: '10000000-0000-0000-0000-000000000002', fullName: 'Verification Attendant',
    email: 'admin@verification.test', role: 'ParkingAdmin', hasDisabilityPermit: false,
  } }, version: 0 }));
  const response = await fetch('http://127.0.0.1:5099/api/bookings', { method: 'POST',
    headers: { Authorization: 'Bearer verification-driver', 'Content-Type': 'application/json' },
    body: JSON.stringify({ slotId: '30000000-0000-0000-0000-000000000001',
      vehiclePlate: 'ABC-1234', startTime: new Date().toISOString(), endTime: new Date(Date.now() + 7200000).toISOString() }),
  });
  const payload = await response.json();
  return { status: response.status, bookingId: payload.data?.id, error: payload.error?.message };
})()
