import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/services/api_config.dart';
import 'package:mobile/services/booking_service.dart';
import 'package:mobile/services/permit_service.dart';
import 'package:mobile/services/penalty_service.dart';
import 'package:mobile/services/zone_service.dart';

void main() {
  setUp(() => ApiConfig.setBaseUrl('https://parking.test'));
  test(
      'booking history falls back on an older backend and preserves pagination',
      () async {
    final paths = <String>[];
    final service = BookingService(client: MockClient((request) async {
      paths.add(request.url.path);
      expect(request.url.queryParameters['page'], '2');
      expect(request.url.queryParameters['pageSize'], '10');
      if (request.url.path == '/api/customer/bookings') {
        return http.Response('', 404);
      }
      return http.Response(
          '{"data":{"items":[],"totalCount":25,"page":2,"pageSize":10}}', 200);
    }));
    final result = await service.getUserBookings(page: 2);
    expect(paths, ['/api/customer/bookings', '/api/bookings/user']);
    expect(result.page, 2);
    expect(result.hasNextPage, isTrue);
  });
  test('a missing legacy route remains an error instead of empty bookings',
      () async {
    final service =
        BookingService(client: MockClient((_) async => http.Response('', 404)));
    await expectLater(service.getUserBookings(), throwsA(isA<Exception>()));
  });
  test('booking history includes real session fees and payment state',
      () async {
    final service = BookingService(client: MockClient((request) async {
      expect(request.url.path, '/api/customer/bookings');
      return http.Response(
          jsonEncode({
            'data': {
              'items': [
                {
                  'id': 'booking',
                  'userId': 'driver',
                  'slotId': 'slot',
                  'startTime': '2026-10-04T08:00:00Z',
                  'endTime': '2026-10-04T10:00:00Z',
                  'status': 'Completed',
                  'currency': 'LKR',
                  'isPaid': true,
                  'vehiclePlate': 'ABC1234',
                  'zoneId': 'zone',
                  'zoneName': 'Ground',
                  'session': {
                    'id': 'session',
                    'checkInTime': '2026-10-04T08:00:00Z',
                    'checkOutTime': '2026-10-04T10:00:00Z',
                    'totalFee': 250,
                    'penaltyFee': 50
                  }
                }
              ],
              'totalCount': 12,
              'page': 1,
              'pageSize': 10
            }
          }),
          200);
    }));
    final result = await service.getUserBookings();
    expect(result.hasNextPage, isTrue);
    expect(result.items.single.vehiclePlate, 'ABC1234');
    expect(result.items.single.session!.totalFee, 250);
    expect(result.items.single.isPaid, isTrue);
  });
  test('permit upload sends document and backend issuing-authority field',
      () async {
    final service = PermitService(client: MockClient((request) async {
      expect(request.url.path, '/api/users/driver/permits');
      final payload = jsonDecode(request.body);
      expect(payload['jurisdiction'], 'Colombo');
      expect(payload['documentBase64'], 'image');
      return http.Response(
          jsonEncode({
            'data': {
              'id': 'permit',
              'userId': 'driver',
              'permitNumber': 'P1',
              'jurisdiction': 'Colombo',
              'expiryDate': '2027-01-01T00:00:00Z',
              'status': 'Verified'
            }
          }),
          200);
    }));
    final permit = await service.submitPermit(
        userId: 'driver',
        permitNumber: 'P1',
        issuingAuthority: 'Colombo',
        expiryDate: DateTime.utc(2027),
        documentBase64: 'image');
    expect(permit.issuingAuthority, 'Colombo');
    expect(permit.status, 'Verified');
  });
  test('dispute sends the notes accepted by the API', () async {
    final service = PenaltyService(client: MockClient((request) async {
      expect(jsonDecode(request.body)['notes'], 'My vehicle left on time.');
      return http.Response('{"data":{}}', 200);
    }));
    expect(await service.disputePenalty('penalty', 'My vehicle left on time.'),
        isTrue);
  });
  test('zone filtering preserves server pagination', () async {
    final service = ZoneService(client: MockClient((request) async {
      expect(request.url.queryParameters['filter'], 'EV Charging');
      return http.Response(
          '{"data":{"items":[],"totalCount":25,"page":2,"pageSize":10}}', 200);
    }));
    final result = await service.getZones(page: 2, filter: 'EV Charging');
    expect(result.page, 2);
    expect(result.totalCount, 25);
    expect(result.hasNextPage, isTrue);
  });
  test('failed booking list reports server error instead of an empty history',
      () async {
    final service = BookingService(
        client: MockClient((_) async => http.Response(
            '{"error":{"message":"Database unavailable"}}', 503)));
    await expectLater(
        service.getUserBookings(),
        throwsA(
            predicate((e) => e.toString().contains('Database unavailable'))));
  });
}
