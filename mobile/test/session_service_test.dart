import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/services/session_service.dart';

void main() {
  group('SessionService Tests', () {
    test('checkIn successfully parses 200 response', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, equals('/api/sessions/check-in'));
        final responseJson = {
          'id': 'sess-123',
          'bookingId': 'bk-9912',
          'slotNumber': 'A-102',
          'zoneName': 'Zone A',
          'checkInTime': '2026-09-26T12:00:00Z',
          'status': 'Active',
          'overstayMinutes': 0,
          'totalFee': 0.0,
          'penaltyFee': 0.0,
          'hourlyRate': 5.0,
        };
        return http.Response(jsonEncode(responseJson), 200);
      });

      final service = SessionService(client: mockClient);
      final session = await service.checkIn(bookingId: 'bk-9912', slotId: 'slot-a102');

      expect(session.id, equals('sess-123'));
      expect(session.bookingId, equals('bk-9912'));
      expect(session.slotNumber, equals('A-102'));
      expect(session.status, equals('Active'));
      expect(session.hourlyRate, equals(5.0));
    });

    test('checkIn throws SessionApiException with 404 on unknown booking', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode({'message': 'Booking not found.'}), 404);
      });

      final service = SessionService(client: mockClient);

      expect(
        () async => await service.checkIn(bookingId: 'unknown-id'),
        throwsA(isA<SessionApiException>().having((e) => e.statusCode, 'statusCode', 404)),
      );
    });

    test('checkIn throws SessionApiException with 409 on duplicate active session', () async {
      final mockClient = MockClient((request) async {
        return http.Response(jsonEncode({'message': 'An active parking session is already running.'}), 409);
      });

      final service = SessionService(client: mockClient);

      expect(
        () async => await service.checkIn(bookingId: 'already-active-id'),
        throwsA(isA<SessionApiException>().having((e) => e.statusCode, 'statusCode', 409)),
      );
    });

    test('checkOut successfully closes active session', () async {
      final mockClient = MockClient((request) async {
        expect(request.url.path, equals('/api/sessions/check-out'));
        final responseJson = {
          'id': 'sess-123',
          'bookingId': 'bk-9912',
          'slotNumber': 'A-102',
          'zoneName': 'Zone A',
          'checkInTime': '2026-09-26T10:00:00Z',
          'checkOutTime': '2026-09-26T12:00:00Z',
          'status': 'Completed',
          'overstayMinutes': 0,
          'totalFee': 10.0,
          'penaltyFee': 0.0,
          'hourlyRate': 5.0,
          'receiptPdfUrl': '/receipts/sess-123.pdf',
        };
        return http.Response(jsonEncode(responseJson), 200);
      });

      final service = SessionService(client: mockClient);
      final session = await service.checkOut(sessionId: 'sess-123');

      expect(session.status, equals('Completed'));
      expect(session.totalFee, equals(10.0));
      expect(session.receiptPdfUrl, isNotNull);
    });
  });
}
