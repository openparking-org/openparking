# OpenParking — Driver Flutter App
## Pages, Routing & Feature Specification

**Platform:** Flutter 3 / Dart  
**State Management:** Riverpod 2 (`flutter_riverpod`, `riverpod_annotation`, `build_runner`)  
**Routing:** GoRouter (deep linking, auth guards, redirect logic)  
**Key Packages:** `mapbox_maps_flutter`, `mobile_scanner`, `flutter_secure_storage`, `flutter_dotenv`

---

## Routing Guard Logic

```
/  (SplashScreen)
  ├── No token + First Launch  →  /onboarding
  ├── No token                 →  /login
  └── Has valid token          →  /home
```

All routes under the main shell (`/home`, `/map`, `/bookings`, `/account`) require a valid JWT stored in `flutter_secure_storage`. GoRouter's `redirect` callback handles unauthenticated access automatically.

---

## 🔓 Auth Flow — No Auth Required

| Route | Screen | Description |
|---|---|---|
| `/` | `SplashScreen` | Animated logo; checks token validity; redirects to appropriate screen |
| `/onboarding` | `OnboardingScreen` | 3-slide intro carousel for first-time users |
| `/login` | `LoginScreen` | Email + password form — calls `POST /api/users/login`; stores JWT on success |
| `/register` | `RegisterScreen` | Driver account creation — calls `POST /api/users/register` |
| `/forgot-password` | `ForgotPasswordScreen` | Submits email to trigger Resend password reset link |

---

## 🏠 Main Shell — Bottom Navigation (Requires Auth)

The app uses a persistent `BottomNavigationBar` with 4 tabs managed by GoRouter `ShellRoute`.

| Tab Index | Icon | Label | Root Route |
|---|---|---|---|
| 0 | 🏠 Home | Home | `/home` |
| 1 | 🗺️ Map | Find Parking | `/map` |
| 2 | 📋 Bookings | My Bookings | `/bookings` |
| 3 | 👤 Account | Account | `/account` |

---

## 🏠 Tab 0 — Home

| Route | Screen | Description |
|---|---|---|
| `/home` | `HomeScreen` | Dashboard: active session card (if any), nearby zones list, quick action buttons (Find Parking, Scan QR, My Penalties) |

---

## 🗺️ Tab 1 — Find Parking (Map)

| Route | Screen | Description |
|---|---|---|
| `/map` | `MapScreen` | Live Mapbox map with zone marker pins showing real-time availability counts; search bar; GPS centering button |
| `/map/zone/:id` | `ZoneDetailScreen` | Zone details: name, address, slot grid, price/hr, distance, operating hours, photos |
| `/map/zone/:id/book` | `BookingFormScreen` | Select registered vehicle, set start date/time, set duration; shows live price estimate |
| `/map/zone/:id/book/confirm` | `BookingConfirmScreen` | Full booking summary: zone, slot, vehicle, times, itemized price breakdown; confirm button |
| `/map/zone/:id/book/payment` | `PaymentScreen` | Select saved card or add new one; final charge amount displayed; submit payment |
| `/map/zone/:id/book/success` | `BookingSuccessScreen` | Booking confirmed: reference number, QR code for gate entry, "Navigate to Lot" button |

---

## 📋 Tab 2 — Bookings

| Route | Screen | Description |
|---|---|---|
| `/bookings` | `BookingsScreen` | Tabbed list: **Upcoming** / **Active** / **Past** bookings |
| `/bookings/:id` | `BookingDetailScreen` | Full booking details, entry QR code, "Navigate to Lot" button, cancel option (if before start) |
| `/bookings/:id/session` | `ActiveSessionScreen` | Live timer, zone + slot number, "Extend Session" button, "End Session" button |
| `/bookings/:id/extend` | `ExtendSessionScreen` | Select additional duration, see extra cost, confirm payment for extension |
| `/bookings/:id/receipt` | `SessionReceiptScreen` | Final receipt after session ends: duration, total charged, penalty (if any) |

---

## 📷 QR Code Scanner

| Route | Screen | Description |
|---|---|---|
| `/scan` | `QrScannerScreen` | Full-screen camera using `mobile_scanner`; scans QR at gate for entry or exit |
| `/scan/result` | `ScanResultScreen` | Shows result: "Entry Confirmed – Session Started" or "Exit Confirmed – Session Ended" with summary |

---

## 💳 Payments & Wallet

| Route | Screen | Description |
|---|---|---|
| `/payment/methods` | `PaymentMethodsScreen` | List of saved payment cards; set default; delete card |
| `/payment/methods/add` | `AddCardScreen` | Enter card number, expiry, CVV; save securely |
| `/payment/history` | `PaymentHistoryScreen` | Chronological list of all transactions (bookings + penalties + extensions) |
| `/payment/history/:id` | `TransactionDetailScreen` | Full receipt for a single transaction: amount, breakdown, date, reference ID |

---

## 🚨 Penalties & Enforcement

| Route | Screen | Description |
|---|---|---|
| `/penalties` | `PenaltiesScreen` | Tabbed list: **Outstanding** / **Paid** / **Appealing** penalties |
| `/penalties/:id` | `PenaltyDetailScreen` | Penalty details: reason, amount, overstay duration, AI workflow decision summary, deadline |
| `/penalties/:id/pay` | `PenaltyPaymentScreen` | Pay outstanding penalty; select payment method; confirm |
| `/penalties/:id/appeal` | `AppealScreen` | Submit an appeal: free-text reason, optional photo attachment |

---

## 🚗 My Vehicles

| Route | Screen | Description |
|---|---|---|
| `/vehicles` | `VehiclesScreen` | List of registered vehicles; each shows plate, make, size class |
| `/vehicles/add` | `AddVehicleScreen` | Form: licence plate, make/model, vehicle size class (standard / compact / large / motorbike) |
| `/vehicles/:id/edit` | `EditVehicleScreen` | Update plate number or size class |

---

## ♿ Disability Permit

| Route | Screen | Description |
|---|---|---|
| `/permit` | `PermitStatusScreen` | Current permit status: Verified ✅ / Pending ⏳ / Rejected ❌ (driven by AI Validator Agent result) |
| `/permit/upload` | `UploadPermitScreen` | Capture or pick permit image; submits to `POST /api/users/permit`; AI Validator Agent processes it |

---

## 🔔 Notifications

| Route | Screen | Description |
|---|---|---|
| `/notifications` | `NotificationsListScreen` | Chronological list of all push notifications: booking confirmations, overstay warnings, penalty alerts, approval results |

---

## 👤 Tab 3 — Account

| Route | Screen | Description |
|---|---|---|
| `/account` | `AccountScreen` | Profile photo, name, email overview; links to all sub-sections below |
| `/account/profile` | `EditProfileScreen` | Edit name, phone number, email; change profile photo |
| `/account/vehicles` | *(links to `/vehicles`)* | Shortcut to vehicle management |
| `/account/permit` | *(links to `/permit`)* | Shortcut to disability permit status |
| `/account/payment` | *(links to `/payment/methods`)* | Shortcut to payment methods |
| `/account/notifications` | `NotificationsScreen` | Notification history shortcut + quick settings toggle |
| `/account/notifications/settings` | `NotificationSettingsScreen` | Toggle individual types: booking reminders, overstay alerts, penalty notices, email summaries |
| `/account/help` | `HelpScreen` | FAQ accordion, contact support button, link to terms |
| `/account/settings` | `SettingsScreen` | App theme (light/dark/system), map style (standard/satellite/night), language |
| `/account/about` | `AboutScreen` | App version, open-source licenses, privacy policy link |
| `/account/logout` | *(action — no screen)* | Clears JWT from `flutter_secure_storage`, invalidates session, redirects to `/login` |

---

## 📊 Summary

| Category | Screen Count |
|---|---|
| Auth Flow | 5 |
| Main Tabs (Home, Map, Bookings, Account) | 4 |
| Map & Booking Flow | 6 |
| Active Session & QR Scan | 4 |
| Payments & Wallet | 4 |
| Penalties & Enforcement | 4 |
| My Vehicles | 3 |
| Disability Permit | 2 |
| Settings & Account | 7 |
| Notifications | 2 |
| **Total** | **41 screens** |

---

## API Endpoints Referenced

| Screen | Endpoint |
|---|---|
| `LoginScreen` | `POST /api/users/login` |
| `RegisterScreen` | `POST /api/users/register` |
| `MapScreen` | `GET /api/zones?lat=&lng=&radius=` |
| `ZoneDetailScreen` | `GET /api/zones/:id` |
| `BookingFormScreen` | `GET /api/zones/:id/slots/available` |
| `BookingConfirmScreen` | `POST /api/bookings` |
| `PaymentScreen` | `POST /api/payments/charge` |
| `BookingsScreen` | `GET /api/bookings/my` |
| `ActiveSessionScreen` | `GET /api/sessions/active` |
| `ExtendSessionScreen` | `POST /api/sessions/:id/extend` |
| `QrScannerScreen` (Entry) | `POST /api/sessions/start` |
| `QrScannerScreen` (Exit) | `POST /api/sessions/end` |
| `PenaltiesScreen` | `GET /api/penalties/my` |
| `PenaltyPaymentScreen` | `POST /api/penalties/:id/pay` |
| `AppealScreen` | `POST /api/penalties/:id/appeal` |
| `VehiclesScreen` | `GET /api/users/vehicles` |
| `AddVehicleScreen` | `POST /api/users/vehicles` |
| `UploadPermitScreen` | `POST /api/users/permit` |
| `PaymentMethodsScreen` | `GET /api/payments/methods` |
| `AddCardScreen` | `POST /api/payments/methods` |
| `PaymentHistoryScreen` | `GET /api/payments/history` |
