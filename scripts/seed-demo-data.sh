#!/usr/bin/env bash
#
# Seeds a small, realistic dataset for demos and manual testing.
#
# Accounts and bookings are created through the HTTP API rather than SQL, so
# passwords are hashed by the real PasswordHasher and fees are produced by the
# real FeeCalculationService — demo figures cannot drift away from the policy
# held in SystemSettings. Only zones and slots go in over SQL, because those
# slices have no write endpoints yet.
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

DEMO_PASSWORD='Demo1234!'
ZONE_A=22222222-2222-2222-2222-222222222222
ZONE_B=22222222-2222-2222-2222-222222222223

json () { python3 -c "import sys,json; d=json.load(sys.stdin); print(d$1)"; }

echo "==> Waiting for the API at $API"
for _ in $(seq 1 30); do curl -sf "$API/health" >/dev/null 2>&1 && break; sleep 1; done
curl -sf "$API/health" >/dev/null || { echo "API is not responding at $API" >&2; exit 1; }

register () {  # register <email> <full name> -> prints nothing
  curl -s -X POST "$API/api/auth/register" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$1\",\"password\":\"$DEMO_PASSWORD\",\"fullName\":\"$2\",\"phoneNumber\":\"+94770000000\"}" >/dev/null || true
}

login () {  # login <email> -> prints access token
  curl -sf -X POST "$API/api/auth/login" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$1\",\"password\":\"$DEMO_PASSWORD\"}" | json "['accessToken']"
}

echo "==> Creating accounts (password: $DEMO_PASSWORD)"
register "driver.john@example.com" "John Driver"
register "sarah.m@example.com"     "Sarah Mendis"
register "admin@example.com"       "Ada Admin"

# Self-registration always yields a Driver, by design — an elevated role is
# granted administratively, which here means directly in the database.
"$PSQL" -d "$PGDATABASE" -v ON_ERROR_STOP=1 -q \
  -c "UPDATE \"Users\" SET \"Role\" = 1 WHERE \"Email\" = 'admin@example.com';"
echo "    driver.john@example.com, sarah.m@example.com, admin@example.com (ParkingAdmin)"

DRIVER_A_TOKEN=$(login "driver.john@example.com")
DRIVER_B_TOKEN=$(login "sarah.m@example.com")
ADMIN_TOKEN=$(login "admin@example.com")

echo "==> Seeding zones and slots"
"$PSQL" -d "$PGDATABASE" -v ON_ERROR_STOP=1 -q <<SQL
INSERT INTO "Zones" ("Id","Name","Code","Latitude","Longitude","BaseHourlyRate","TotalCapacity","CreatedAt","UpdatedAt") VALUES
  ('$ZONE_A','Zone A - Main Deck','ZA',6.9271,79.8612,6.00,120,now(),now()),
  ('$ZONE_B','Zone B - Rooftop','ZB',6.9280,79.8630,4.50,80, now(),now())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Slots" ("Id","ZoneId","SlotNumber","Type","Status","UpdatedAt")
SELECT md5(z.code || n::text)::uuid, z.id, z.code || '-' || lpad(n::text, 2, '0'),
       CASE WHEN n = 1 THEN 4 WHEN n = 2 THEN 3 ELSE 0 END, 0, now()
FROM (VALUES ('$ZONE_A'::uuid,'A'), ('$ZONE_B'::uuid,'B')) AS z(id, code),
     generate_series(1, 10) AS n
ON CONFLICT ("Id") DO NOTHING;
SQL

slot_id () { "$PSQL" -d "$PGDATABASE" -tAc \
  "SELECT \"Id\" FROM \"Slots\" WHERE \"SlotNumber\" = '$1-$(printf '%02d' "$2")';"; }

book () {  # book <token> <slot-id> <start> <end> [surge]
  curl -sf -X POST "$API/api/bookings" \
    -H 'Content-Type: application/json' -H "Authorization: Bearer $1" \
    -d "{\"slotId\":\"$2\",\"startTime\":\"$3\",\"endTime\":\"$4\",\"surgeMultiplier\":${5:-1.0}}"
}

iso () { date -u -v"$1" +"%Y-%m-%dT%H:00:00Z" 2>/dev/null || date -u -d "$1" +"%Y-%m-%dT%H:00:00Z"; }

echo "==> Creating bookings"

# One reservation already in progress, checked in by staff at the gate.
ACTIVE=$(book "$DRIVER_A_TOKEN" "$(slot_id A 3)" "$(iso -1H)" "$(iso +2H)")
QR=$(printf '%s' "$ACTIVE" | json "['booking']['qrCodeContent']")
curl -sf -X POST "$API/api/sessions/check-in" \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d "{\"qrCodeContent\":\"$QR\"}" >/dev/null
echo "    active session on A-03"

book "$DRIVER_B_TOKEN" "$(slot_id A 5)" "$(iso +3H)" "$(iso +5H)"     >/dev/null && echo "    confirmed A-05"
book "$DRIVER_A_TOKEN" "$(slot_id B 2)" "$(iso +4H)" "$(iso +7H)" 1.5 >/dev/null && echo "    confirmed B-02 (1.5x surge)"

CANCELLED_ID=$(book "$DRIVER_B_TOKEN" "$(slot_id B 7)" "$(iso +8H)" "$(iso +9H)" | json "['booking']['id']")
curl -sf -X POST "$API/api/bookings/$CANCELLED_ID/cancel" -H "Authorization: Bearer $DRIVER_B_TOKEN" >/dev/null
echo "    cancelled B-07"

TOTAL=$(curl -sf "$API/api/bookings?pageSize=1" -H "Authorization: Bearer $ADMIN_TOKEN" | json "['totalCount']")
echo "==> Done. $TOTAL bookings in the database."
echo
echo "    Sign in with any of the three accounts using password: $DEMO_PASSWORD"
