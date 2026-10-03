#!/usr/bin/env bash
#
# Seeds a small, realistic dataset for demos and manual testing.
#
# Reference data (users, zones, slots) goes in over SQL because those slices
# have no write endpoints yet. Bookings and sessions go in over the HTTP API on
# purpose, so the fees shown in the dashboard are produced by the real
# FeeCalculationService rather than hand-written numbers that could drift from
# the policy in SystemSettings.
#
# Usage:  ./scripts/seed-demo-data.sh [API_BASE_URL]
# Env:    PGHOST PGPORT PGUSER PGPASSWORD PGDATABASE
set -euo pipefail

API="${1:-${API_BASE_URL:-http://localhost:5000}}"
export PGHOST="${PGHOST:-localhost}"
export PGPORT="${PGPORT:-5432}"
export PGUSER="${PGUSER:-dev}"
export PGPASSWORD="${PGPASSWORD:-dev}"
PGDATABASE="${PGDATABASE:-openparking}"

PSQL="$(command -v psql || echo /opt/homebrew/opt/postgresql@17/bin/psql)"

DRIVER_A=11111111-1111-1111-1111-111111111111
DRIVER_B=11111111-1111-1111-1111-111111111112
ZONE_A=22222222-2222-2222-2222-222222222222
ZONE_B=22222222-2222-2222-2222-222222222223

echo "==> Waiting for the API at $API"
for _ in $(seq 1 30); do
  curl -sf "$API/health" >/dev/null 2>&1 && break
  sleep 1
done
curl -sf "$API/health" >/dev/null || { echo "API is not responding at $API" >&2; exit 1; }

echo "==> Seeding reference data"
"$PSQL" -d "$PGDATABASE" -v ON_ERROR_STOP=1 -q <<SQL
INSERT INTO "Users" ("Id","Email","PasswordHash","FullName","PhoneNumber","Role","HasDisabilityPermit","CreatedAt","UpdatedAt") VALUES
  ('$DRIVER_A','driver.john@example.com','seed','John Driver','+94770000001',0,false,now(),now()),
  ('$DRIVER_B','sarah.m@example.com','seed','Sarah Mendis','+94770000002',0,true, now(),now())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Zones" ("Id","Name","Code","Latitude","Longitude","BaseHourlyRate","TotalCapacity","CreatedAt","UpdatedAt") VALUES
  ('$ZONE_A','Zone A - Main Deck','ZA',6.9271,79.8612,6.00,120,now(),now()),
  ('$ZONE_B','Zone B - Rooftop','ZB',6.9280,79.8630,4.50,80, now(),now())
ON CONFLICT ("Id") DO NOTHING;

-- Ten slots per zone, with a couple of accessible and EV bays.
INSERT INTO "Slots" ("Id","ZoneId","SlotNumber","Type","Status","UpdatedAt")
SELECT
  md5(z.code || n::text)::uuid,
  z.id,
  z.code || '-' || lpad(n::text, 2, '0'),
  CASE WHEN n = 1 THEN 4 WHEN n = 2 THEN 3 ELSE 0 END,
  0,
  now()
FROM (VALUES ('$ZONE_A'::uuid,'A'), ('$ZONE_B'::uuid,'B')) AS z(id, code),
     generate_series(1, 10) AS n
ON CONFLICT ("Id") DO NOTHING;
SQL

slot_id () {  # slot_id <zone-code> <n>
  "$PSQL" -d "$PGDATABASE" -tAc \
    "SELECT \"Id\" FROM \"Slots\" WHERE \"SlotNumber\" = '$1-$(printf '%02d' "$2")';"
}

book () {  # book <user> <slot-id> <start-iso> <end-iso> [surge]
  curl -sf -X POST "$API/api/bookings" -H 'Content-Type: application/json' \
    -d "{\"userId\":\"$1\",\"slotId\":\"$2\",\"startTime\":\"$3\",\"endTime\":\"$4\",\"surgeMultiplier\":${5:-1.0}}"
}

iso () { date -u -v"$1" +"%Y-%m-%dT%H:00:00Z" 2>/dev/null || date -u -d "$1" +"%Y-%m-%dT%H:00:00Z"; }

echo "==> Creating bookings"

# One reservation in progress: booked from an hour ago, checked in.
ACTIVE=$(book "$DRIVER_A" "$(slot_id A 3)" "$(iso -1H)" "$(iso +2H)")
QR=$(printf '%s' "$ACTIVE" | sed -n 's/.*"qrCodeContent":"\([^"]*\)".*/\1/p')
curl -sf -X POST "$API/api/sessions/check-in" -H 'Content-Type: application/json' \
  -d "{\"qrCodeContent\":\"$QR\"}" >/dev/null
echo "    active session on A-03"

# Two confirmed reservations later today, one of them priced under surge.
book "$DRIVER_B" "$(slot_id A 5)" "$(iso +3H)" "$(iso +5H)"        >/dev/null && echo "    confirmed A-05"
book "$DRIVER_A" "$(slot_id B 2)" "$(iso +4H)" "$(iso +7H)" 1.5     >/dev/null && echo "    confirmed B-02 (1.5x surge)"

# One the driver changed their mind about.
CANCELLED=$(book "$DRIVER_B" "$(slot_id B 7)" "$(iso +8H)" "$(iso +9H)")
CANCELLED_ID=$(printf '%s' "$CANCELLED" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1)
curl -sf -X POST "$API/api/bookings/$CANCELLED_ID/cancel" >/dev/null && echo "    cancelled B-07"

echo "==> Done. $(curl -sf "$API/api/bookings?pageSize=1" | sed -n 's/.*"totalCount":\([0-9]*\).*/\1/p') bookings in the database."
