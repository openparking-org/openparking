import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/utils/qr_payload_parser.dart';

void main() {
  group('QrPayloadParser Tests', () {
    test('parses openparking://session/start standard QR correctly', () {
      const qrData = 'openparking://session/start?bookingId=bk-9912&slotId=slot-a102';
      final result = QrPayloadParser.parse(qrData);

      expect(result.isValid, isTrue);
      expect(result.action, equals(QrActionType.checkIn));
      expect(result.bookingId, equals('bk-9912'));
      expect(result.slotId, equals('slot-a102'));
      expect(result.errorMessage, isNull);
    });

    test('parses openparking://session/check-in scheme correctly', () {
      const qrData = 'openparking://session/check-in?bookingId=00000000-0000-0000-0000-000000000001&slotId=slot-1';
      final result = QrPayloadParser.parse(qrData);

      expect(result.isValid, isTrue);
      expect(result.action, equals(QrActionType.checkIn));
      expect(result.bookingId, equals('00000000-0000-0000-0000-000000000001'));
      expect(result.slotId, equals('slot-1'));
    });

    test('parses openparking://session/check-out scheme correctly', () {
      const qrData = 'openparking://session/check-out?sessionId=sess-12345';
      final result = QrPayloadParser.parse(qrData);

      expect(result.isValid, isTrue);
      expect(result.action, equals(QrActionType.checkOut));
      expect(result.sessionId, equals('sess-12345'));
    });

    test('parses openparking://gate/entry and exit correctly', () {
      final entry = QrPayloadParser.parse('openparking://gate/entry?zoneId=zone-a&bookingId=bk-01');
      expect(entry.isValid, isTrue);
      expect(entry.action, equals(QrActionType.checkIn));
      expect(entry.zoneId, equals('zone-a'));
      expect(entry.bookingId, equals('bk-01'));

      final exit = QrPayloadParser.parse('openparking://gate/exit?sessionId=sess-99');
      expect(exit.isValid, isTrue);
      expect(exit.action, equals(QrActionType.checkOut));
      expect(exit.sessionId, equals('sess-99'));
    });

    test('parses JSON format QR payload correctly', () {
      const jsonEntry = '{"action":"check-in","bookingId":"bk-777","slotId":"slot-b"}';
      final entryResult = QrPayloadParser.parse(jsonEntry);
      expect(entryResult.isValid, isTrue);
      expect(entryResult.action, equals(QrActionType.checkIn));
      expect(entryResult.bookingId, equals('bk-777'));
      expect(entryResult.slotId, equals('slot-b'));

      const jsonExit = '{"action":"check-out","sessionId":"sess-888"}';
      final exitResult = QrPayloadParser.parse(jsonExit);
      expect(exitResult.isValid, isTrue);
      expect(exitResult.action, equals(QrActionType.checkOut));
      expect(exitResult.sessionId, equals('sess-888'));
    });

    test('parses raw UUID correctly as check-in identifier', () {
      const rawUuid = 'e87b7a7c-47b2-4d7d-8ea0-3f487e411b0e';
      final result = QrPayloadParser.parse(rawUuid);

      expect(result.isValid, isTrue);
      expect(result.action, equals(QrActionType.checkIn));
      expect(result.bookingId, equals(rawUuid));
    });

    test('handles empty and null values gracefully without throwing', () {
      final nullResult = QrPayloadParser.parse(null);
      expect(nullResult.isValid, isFalse);
      expect(nullResult.errorMessage, contains('Empty'));

      final emptyResult = QrPayloadParser.parse('   ');
      expect(emptyResult.isValid, isFalse);
      expect(emptyResult.errorMessage, contains('Empty'));
    });

    test('handles malformed / arbitrary text gracefully without throwing', () {
      final invalidResult = QrPayloadParser.parse('https://randomwebsite.com/page?id=123');
      expect(invalidResult.isValid, isFalse);
      expect(invalidResult.errorMessage, contains('Unsupported QR format'));
    });
  });
}
