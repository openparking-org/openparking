import 'dart:convert';

enum QrActionType {
  checkIn,
  checkOut,
  unknown,
}

class QrScanResult {
  final bool isValid;
  final QrActionType action;
  final String? bookingId;
  final String? slotId;
  final String? sessionId;
  final String? zoneId;
  final String rawValue;
  final String? errorMessage;

  const QrScanResult({
    required this.isValid,
    required this.action,
    required this.rawValue,
    this.bookingId,
    this.slotId,
    this.sessionId,
    this.zoneId,
    this.errorMessage,
  });

  factory QrScanResult.invalid(String rawValue, String errorMessage) {
    return QrScanResult(
      isValid: false,
      action: QrActionType.unknown,
      rawValue: rawValue,
      errorMessage: errorMessage,
    );
  }
}

class QrPayloadParser {
  /// Safely parses and validates any scanned QR payload without throwing exceptions.
  static QrScanResult parse(String? raw) {
    if (raw == null || raw.trim().isEmpty) {
      return QrScanResult.invalid('', 'Empty QR code detected.');
    }

    final trimmed = raw.trim();

    // 1. Try URI parsing for openparking:// schemes
    if (trimmed.startsWith('openparking://') ||
        trimmed.startsWith('http://') ||
        trimmed.startsWith('https://')) {
      try {
        final uri = Uri.parse(trimmed);
        final host = uri.host.toLowerCase();
        final path = uri.path.toLowerCase();
        final params = uri.queryParameters;

        final bookingId =
            params['bookingId'] ?? params['booking_id'] ?? params['booking'];
        final slotId = params['slotId'] ?? params['slot_id'] ?? params['slot'];
        final sessionId =
            params['sessionId'] ?? params['session_id'] ?? params['session'];
        final zoneId = params['zoneId'] ?? params['zone_id'] ?? params['zone'];

        // Determine action based on URI host / path
        if (host == 'session' &&
            (path == '/start' || path == '/check-in' || path.isEmpty)) {
          if (bookingId == null && slotId == null) {
            return QrScanResult.invalid(
                trimmed, 'QR code is missing a booking or slot identifier.');
          }
          return QrScanResult(
            isValid: true,
            action: QrActionType.checkIn,
            bookingId: bookingId,
            slotId: slotId,
            zoneId: zoneId,
            rawValue: trimmed,
          );
        }

        if (host == 'session' && path == '/check-out') {
          if (sessionId == null && bookingId == null) {
            return QrScanResult.invalid(trimmed,
                'QR code is missing an active session identifier for check-out.');
          }
          return QrScanResult(
            isValid: true,
            action: QrActionType.checkOut,
            sessionId: sessionId,
            bookingId: bookingId,
            zoneId: zoneId,
            rawValue: trimmed,
          );
        }

        if (host == 'gate') {
          if (path == '/entry') {
            return QrScanResult(
              isValid: true,
              action: QrActionType.checkIn,
              bookingId: bookingId,
              slotId: slotId,
              zoneId: zoneId,
              rawValue: trimmed,
            );
          }
          if (path == '/exit') {
            return QrScanResult(
              isValid: true,
              action: QrActionType.checkOut,
              sessionId: sessionId,
              bookingId: bookingId,
              zoneId: zoneId,
              rawValue: trimmed,
            );
          }
        }

        // Generic query parameter fallback
        if (bookingId != null || slotId != null) {
          return QrScanResult(
            isValid: true,
            action: QrActionType.checkIn,
            bookingId: bookingId,
            slotId: slotId,
            zoneId: zoneId,
            rawValue: trimmed,
          );
        }
      } catch (_) {
        // Fall through to JSON or text checks
      }
    }

    // 2. Try JSON parsing
    if (trimmed.startsWith('{') && trimmed.endsWith('}')) {
      try {
        final decoded = jsonDecode(trimmed);
        if (decoded is Map<String, dynamic>) {
          final actionStr = (decoded['action'] ?? decoded['type'] ?? '')
              .toString()
              .toLowerCase();
          final bookingId =
              (decoded['bookingId'] ?? decoded['booking_id'] ?? decoded['id'])
                  ?.toString();
          final slotId = (decoded['slotId'] ?? decoded['slot_id'])?.toString();
          final sessionId =
              (decoded['sessionId'] ?? decoded['session_id'])?.toString();
          final zoneId = (decoded['zoneId'] ?? decoded['zone_id'])?.toString();

          final isCheckOut = actionStr.contains('exit') ||
              actionStr.contains('check-out') ||
              actionStr == 'checkout';

          if (isCheckOut) {
            return QrScanResult(
              isValid: true,
              action: QrActionType.checkOut,
              sessionId: sessionId,
              bookingId: bookingId,
              zoneId: zoneId,
              rawValue: trimmed,
            );
          }

          if (bookingId != null || slotId != null) {
            return QrScanResult(
              isValid: true,
              action: QrActionType.checkIn,
              bookingId: bookingId,
              slotId: slotId,
              zoneId: zoneId,
              rawValue: trimmed,
            );
          }
        }
      } catch (_) {
        // Not valid JSON
      }
    }

    // 3. Fallback: check if raw string looks like a UUID or booking code
    final uuidRegex = RegExp(
        r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$');
    final bookingCodeRegex = RegExp(r'^[A-Za-z0-9\-_]{4,40}$');

    if (uuidRegex.hasMatch(trimmed) || bookingCodeRegex.hasMatch(trimmed)) {
      return QrScanResult(
        isValid: true,
        action: QrActionType.checkIn,
        bookingId: trimmed,
        rawValue: trimmed,
      );
    }

    return QrScanResult.invalid(trimmed,
        'Unsupported QR format: "$trimmed" is not a recognized OpenParking code.');
  }
}
