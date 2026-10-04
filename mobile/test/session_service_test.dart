import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/services/session_service.dart';
import 'package:mobile/services/api_config.dart';

void main() {
  setUp(() => ApiConfig.setBaseUrl('https://parking.test'));
  test('active session preserves currency and vehicle number', () async {
    final service = SessionService(client: MockClient((request) async {
      expect(request.method, 'GET');
      expect(request.url.path, '/api/sessions/active');
      return http.Response(
          jsonEncode({
            'data': {
              'id': 'session',
              'bookingId': 'booking',
              'checkInTime': '2026-10-04T09:00:00Z',
              'status': 'Active',
              'currency': 'LKR',
              'vehiclePlate': 'ABC1234'
            }
          }),
          200);
    }));
    final session = await service.getActiveSession();
    expect(session!.currency, 'LKR');
    expect(session.vehiclePlate, 'ABC1234');
  });
  test('no active session returns null only on 404', () async {
    final service =
        SessionService(client: MockClient((_) async => http.Response('', 404)));
    expect(await service.getActiveSession(), isNull);
  });
  test('server failures are reported instead of showing no active session',
      () async {
    final service = SessionService(
        client: MockClient((_) async => http.Response(
            jsonEncode({
              'error': {'message': 'Server unavailable'}
            }),
            503)));
    await expectLater(
        service.getActiveSession(),
        throwsA(isA<SessionApiException>()
            .having((e) => e.message, 'message', 'Server unavailable')));
  });
  test('expired authentication invokes sign-out handler', () async {
    var signedOut = false;
    ApiConfig.onUnauthorized = () => signedOut = true;
    final service =
        SessionService(client: MockClient((_) async => http.Response('', 401)));
    await expectLater(
        service.getActiveSession(), throwsA(isA<SessionApiException>()));
    expect(signedOut, isTrue);
    ApiConfig.onUnauthorized = null;
  });
}
