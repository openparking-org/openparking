import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/core/providers/zone_provider.dart';
import 'package:mobile/modules/space_availability/zone_discovery_screen.dart';
import 'package:mobile/services/parking_recommendation_service.dart';
import 'package:mobile/services/zone_service.dart';

class FakeLocation extends ParkingLocationService {
  int calls = 0;
  final bool denied;
  FakeLocation({this.denied = false});
  @override
  Future<ParkingLocation> current() async {
    calls++;
    if (denied) {
      throw Exception('Location permission denied. Browse zones below.');
    }
    return const ParkingLocation(6.9, 79.8, 10);
  }
}

void main() {
  for (final denied in [false, true]) {
    testWidgets('Explicit Find Parking runs GPS and agent; denied=$denied',
        (tester) async {
      final location = FakeLocation(denied: denied);
      int agentCalls = 0;
      final agent =
          ParkingRecommendationService(client: MockClient((request) async {
        agentCalls++;
        final body = jsonDecode(request.body);
        expect(body['latitude'], 6.9);
        expect(body['slotType'], 'Standard');
        return http.Response(
            jsonEncode({
              'data': {
                'requestId': 'test-run',
                'message': 'Select a zone',
                'recommendations': [
                  {
                    'zoneId': 'zone',
                    'name': 'Recommended City Parking',
                    'currency': 'LKR',
                    'hourlyRate': 100,
                    'distanceMeters': 200,
                    'availableCount': 2,
                    'reason': 'Closest available parking'
                  }
                ]
              }
            }),
            200);
      }));
      await tester.pumpWidget(ProviderScope(overrides: [
        parkingLocationProvider.overrideWithValue(location),
        parkingRecommendationProvider.overrideWithValue(agent),
        zoneServiceProvider.overrideWithValue(ZoneService(
            client: MockClient((_) async => http.Response(
                '{"data":{"items":[],"totalCount":0,"page":1,"pageSize":10}}',
                200)))),
      ], child: const MaterialApp(home: ZoneDiscoveryScreen())));
      await tester.pumpAndSettle();
      expect(location.calls,
          0); // Merely mounting the background tab must not request location.
      await tester.tap(find.text('Find parking near me'));
      await tester.pumpAndSettle();
      expect(location.calls, 1);
      expect(agentCalls, denied ? 0 : 1);
      expect(
          find.text(denied
              ? 'Location permission denied. Browse zones below.'
              : 'Recommended City Parking'),
          findsOneWidget);
      expect(find.text('Browse all parking zones'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }
}
