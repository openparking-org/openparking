import 'dart:convert';
import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/core/providers/zone_provider.dart';
import 'package:mobile/modules/space_availability/zone_discovery_screen.dart';
import 'package:mobile/services/zone_service.dart';

void main() {
  test('Loading reservation details does not change discovery state', () async {
    final detail = Completer<http.Response>();
    final container = ProviderContainer(overrides: [
      zoneServiceProvider
          .overrideWithValue(ZoneService(client: MockClient((request) async {
        if (request.url.path.endsWith('/zone')) return detail.future;
        return http.Response(
            jsonEncode({
              'data': {'items': [], 'totalCount': 0, 'page': 1, 'pageSize': 10}
            }),
            200);
      }))),
    ]);
    addTearDown(container.dispose);
    container.read(zoneListProvider);
    await Future<void>.delayed(Duration.zero);
    final discovery = container.read(zoneListProvider);
    final result = container.read(zoneDetailProvider('zone').future);
    expect(identical(container.read(zoneListProvider), discovery), isTrue);
    detail.complete(http.Response('null', 200));
    await expectLater(result, throwsA(isA<Exception>()));
    expect(container.read(zoneDetailProvider('zone')).hasError, isTrue);
    expect(identical(container.read(zoneListProvider), discovery), isTrue);
  });

  for (final scenario in ['empty', 'populated', 'error']) {
    testWidgets('Find Parking renders $scenario results in the second tab',
        (tester) async {
      tester.view.physicalSize = const Size(320, 480);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(ProviderScope(
        overrides: [
          zoneServiceProvider.overrideWithValue(ZoneService(
            client: MockClient((_) async => http.Response(
                  jsonEncode(scenario == 'error'
                      ? {'message': 'Server unavailable'}
                      : {
                          'data': {
                            'items': scenario == 'empty'
                                ? []
                                : [
                                    {
                                      'id': 'zone',
                                      'name': 'City Parking',
                                      'code': 'CITY',
                                      'totalCapacity': 10,
                                      'availableCount': 5
                                    }
                                  ],
                            'totalCount': scenario == 'empty' ? 0 : 1,
                            'page': 1,
                            'pageSize': 10
                          }
                        }),
                  scenario == 'error' ? 503 : 200,
                )),
          )),
        ],
        child: const MaterialApp(
            home: Scaffold(
                body: IndexedStack(
          index: 1,
          children: [SizedBox(), ZoneDiscoveryScreen()],
        ))),
      ));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.text('Find Parking'), findsOneWidget);
      expect(
          find.text(scenario == 'empty'
              ? 'No Parking Lots Found'
              : scenario == 'populated'
                  ? 'City Parking'
                  : 'Exception: Server unavailable'),
          findsOneWidget);
    });
  }
}
