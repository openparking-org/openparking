import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/core/providers/booking_provider.dart';
import 'package:mobile/core/providers/permit_provider.dart';
import 'package:mobile/core/providers/session_provider.dart';
import 'package:mobile/core/providers/zone_provider.dart';
import 'package:mobile/core/theme.dart';
import 'package:mobile/main.dart';
import 'package:mobile/modules/booking/create_booking_screen.dart';
import 'package:mobile/modules/user_access/login_screen.dart';
import 'package:mobile/services/booking_service.dart';
import 'package:mobile/services/permit_service.dart';
import 'package:mobile/services/session_service.dart';
import 'package:mobile/services/parking_recommendation_service.dart';
import 'package:mobile/services/zone_service.dart';
import 'package:mobile/services/signalr_service.dart';

const zone = {
  'id': 'zone',
  'name': 'City Centre Parking',
  'code': 'CITY',
  'totalCapacity': 30,
  'availableCount': 12,
  'baseHourlyRate': 200,
  'currency': 'LKR',
  'slots': [
    {
      'id': 'slot',
      'slotNumber': 'A-01',
      'type': 'Standard',
      'status': 'Available',
      'floor': 1
    }
  ]
};

class PreviewBookings extends BookingNotifier {
  PreviewBookings(super.service) {
    fetchUserBookings();
  }
}

class PreviewLocation extends ParkingLocationService {
  @override
  Future<ParkingLocation> current() async => throw Exception(
      'Location permission is needed for nearby recommendations.');
}

class MockSignalRService extends SignalRService {
  @override
  Future<void> connect() async {}
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() async {
    final font = FontLoader(AppTheme.fontFamily)
      ..addFont(rootBundle.load('assets/fonts/PlusJakartaSans.ttf'));
    await font.load();
    final icons = FontLoader('MaterialIcons')
      ..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'));
    await icons.load();
  });
  setUp(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
            const MethodChannel('plugins.it_nomads.com/flutter_secure_storage'),
            (_) async => null);
  });

  for (final size in [
    const Size(320, 640),
    const Size(390, 844),
    const Size(800, 1024)
  ]) {
    for (final scale in [1.0, 1.5, 2.0]) {
      testWidgets(
          'All tabs and reservation form fit ${size.width}px at text scale $scale',
          (tester) async {
        tester.view.physicalSize = size;
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final client = MockClient((request) async {
          Object? data;
          if (request.url.path.endsWith('/permit')) {
            return http.Response('', 404);
          }
          if (request.url.path.contains('bookings')) {
            data = {
              'items': [
                {
                  'id': 'booking',
                  'userId': 'driver',
                  'slotId': 'slot',
                  'zoneId': 'zone',
                  'zoneName': 'City Centre Parking',
                  'vehiclePlate': 'ABC-1234',
                  'status': 'Confirmed',
                  'startTime': '2026-10-05T08:00:00Z',
                  'endTime': '2026-10-05T10:00:00Z',
                  'estimatedFee': 400,
                  'currency': 'LKR',
                  'slot': (zone['slots'] as List).first,
                }
              ],
              'totalCount': 1,
              'page': 1,
              'pageSize': 10
            };
          } else if (request.url.path.endsWith('/zone')) {
            data = zone;
          } else {
            data = {
              'items': [zone],
              'totalCount': 1,
              'page': 1,
              'pageSize': 10
            };
          }
          return http.Response(jsonEncode({'data': data}), 200);
        });
        final container = ProviderContainer(overrides: [
          parkingLocationProvider.overrideWithValue(PreviewLocation()),
          zoneServiceProvider.overrideWithValue(ZoneService(client: client)),
          bookingProvider.overrideWith(
              (ref) => PreviewBookings(BookingService(client: client))),
          activeSessionProvider.overrideWith((ref) =>
              SessionStateNotifier(SessionService(client: client))
                ..updateSession(null)),
          permitServiceProvider
              .overrideWithValue(PermitService(client: client)),
          signalRServiceProvider.overrideWithValue(MockSignalRService()),
        ]);
        addTearDown(container.dispose);
        Future<void> show(Widget screen) async {
          await tester.pumpWidget(UncontrolledProviderScope(
              container: container,
              child: MaterialApp(
                debugShowCheckedModeBanner: false,
                theme: AppTheme.lightTheme,
                builder: (context, child) => MediaQuery(
                    data: MediaQuery.of(context)
                        .copyWith(textScaler: TextScaler.linear(scale)),
                    child: child!),
                home: screen,
              )));
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
        }

        await show(const MainNavigationScreen());
        Future<void> preview(String name) async {
          if (const bool.fromEnvironment('UI_PREVIEWS') &&
              size.width == 390 &&
              scale == 1) {
            await expectLater(find.byType(MaterialApp),
                matchesGoldenFile('../design/preview-$name.png'));
          }
        }

        await preview('home');
        for (final label in [
          'Find Parking',
          'My Bookings',
          'Account',
          'Home'
        ]) {
          await tester.tap(find.descendant(
              of: find.byType(NavigationBar), matching: find.text(label)));
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
          await preview(label.toLowerCase().replaceAll(' ', '-'));
        }
        await show(const CreateBookingScreen(zoneId: 'zone'));
        await preview('reservation');
        await tester.drag(
            find.byType(SingleChildScrollView).first, const Offset(0, -1500));
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        expect(find.text('Confirm & Reserve'), findsOneWidget);
        await show(const LoginScreen());
      });
    }
  }
}
