import 'dart:convert';
import 'dart:io' show SocketException;
import 'package:http/http.dart' as http;
import 'api_config.dart';

class SessionApiException implements Exception {
  final int statusCode;
  final String message;

  SessionApiException(this.statusCode, this.message);

  @override
  String toString() => message;
}

class ParkingSessionModel {
  final String id;
  final String bookingId;
  final String slotNumber;
  final String zoneName;
  final String? zoneId;
  final DateTime checkInTime;
  final DateTime? checkOutTime;
  final String status;
  final int overstayMinutes;
  final double totalFee;
  final double penaltyFee;
  final double hourlyRate;
  final String? receiptPdfUrl;

  const ParkingSessionModel({
    required this.id,
    required this.bookingId,
    required this.slotNumber,
    required this.zoneName,
    this.zoneId,
    required this.checkInTime,
    this.checkOutTime,
    required this.status,
    required this.overstayMinutes,
    required this.totalFee,
    required this.penaltyFee,
    required this.hourlyRate,
    this.receiptPdfUrl,
  });

  factory ParkingSessionModel.fromJson(Map<String, dynamic> json) {
    return ParkingSessionModel(
      id: json['id']?.toString() ?? '',
      bookingId: json['bookingId']?.toString() ?? '',
      slotNumber: json['slotNumber']?.toString() ?? 'Unknown Slot',
      zoneName: json['zoneName']?.toString() ?? 'Campus Parking',
      zoneId: json['zoneId']?.toString(),
      checkInTime: json['checkInTime'] != null
          ? DateTime.tryParse(json['checkInTime'].toString()) ?? DateTime.now()
          : DateTime.now(),
      checkOutTime: json['checkOutTime'] != null
          ? DateTime.tryParse(json['checkOutTime'].toString())
          : null,
      status: json['status']?.toString() ?? 'Active',
      overstayMinutes: json['overstayMinutes'] is int
          ? json['overstayMinutes'] as int
          : int.tryParse(json['overstayMinutes']?.toString() ?? '0') ?? 0,
      totalFee: json['totalFee'] is num
          ? (json['totalFee'] as num).toDouble()
          : double.tryParse(json['totalFee']?.toString() ?? '0.0') ?? 0.0,
      penaltyFee: json['penaltyFee'] is num
          ? (json['penaltyFee'] as num).toDouble()
          : double.tryParse(json['penaltyFee']?.toString() ?? '0.0') ?? 0.0,
      hourlyRate: json['hourlyRate'] is num
          ? (json['hourlyRate'] as num).toDouble()
          : double.tryParse(json['hourlyRate']?.toString() ?? '5.0') ?? 5.0,
      receiptPdfUrl: json['receiptPdfUrl']?.toString(),
    );
  }
}

class SessionService {
  final http.Client _client;

  SessionService({http.Client? client}) : _client = client ?? http.Client();

  /// Performs QR check-in against ASP.NET Core endpoint POST /api/sessions/check-in.
  Future<ParkingSessionModel> checkIn({
    String? bookingId,
    String? slotId,
    String? bookingCode,
    String? userId,
  }) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/sessions/check-in');
    final payload = <String, dynamic>{};
    if (bookingId != null && bookingId.isNotEmpty)
      payload['bookingId'] = bookingId;
    if (slotId != null && slotId.isNotEmpty) payload['slotId'] = slotId;
    if (bookingCode != null && bookingCode.isNotEmpty)
      payload['bookingCode'] = bookingCode;
    if (userId != null && userId.isNotEmpty) payload['userId'] = userId;

    try {
      final response = await _client.post(
        url,
        headers: ApiConfig.headers,
        body: jsonEncode(payload),
      );

      return _handleResponse(response);
    } on SocketException {
      throw SessionApiException(0,
          'Unable to connect to OpenParking server. Please verify network or backend status.');
    } on http.ClientException catch (e) {
      throw SessionApiException(0, 'Network communication error: ${e.message}');
    }
  }

  /// Performs QR check-out against ASP.NET Core endpoint POST /api/sessions/check-out.
  Future<ParkingSessionModel> checkOut({
    String? sessionId,
    String? bookingId,
  }) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/sessions/check-out');
    final payload = <String, dynamic>{};
    if (sessionId != null && sessionId.isNotEmpty)
      payload['sessionId'] = sessionId;
    if (bookingId != null && bookingId.isNotEmpty)
      payload['bookingId'] = bookingId;

    try {
      final response = await _client.post(
        url,
        headers: ApiConfig.headers,
        body: jsonEncode(payload),
      );

      return _handleResponse(response);
    } on SocketException {
      throw SessionApiException(0,
          'Unable to connect to OpenParking server. Please check your network connection.');
    } on http.ClientException catch (e) {
      throw SessionApiException(0, 'Network communication error: ${e.message}');
    }
  }

  /// Fetches the currently active session for driver tracker screen.
  Future<ParkingSessionModel?> getActiveSession({
    String? userId,
    String? bookingId,
  }) async {
    final queryParams = <String, String>{};
    if (userId != null && userId.isNotEmpty) queryParams['userId'] = userId;
    if (bookingId != null && bookingId.isNotEmpty)
      queryParams['bookingId'] = bookingId;

    final baseUri = Uri.parse('${ApiConfig.baseUrl}/api/sessions/active');
    final url = baseUri.replace(
        queryParameters: queryParams.isNotEmpty ? queryParams : null);

    try {
      final response = await _client.get(url, headers: ApiConfig.headers);
      if (response.statusCode == 404) {
        return null;
      }
      return _handleResponse(response);
    } catch (_) {
      return null;
    }
  }

  ParkingSessionModel _handleResponse(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      final data = jsonDecode(response.body);
      if (data is Map<String, dynamic>) {
        return ParkingSessionModel.fromJson(data);
      }
      throw SessionApiException(
          response.statusCode, 'Unexpected server response format.');
    }

    String errorMsg = 'Operation failed with status ${response.statusCode}.';
    try {
      final errData = jsonDecode(response.body);
      if (errData is Map && errData['message'] != null) {
        errorMsg = errData['message'].toString();
      }
    } catch (_) {}

    switch (response.statusCode) {
      case 400:
        throw SessionApiException(
            400, errorMsg.isNotEmpty ? errorMsg : 'Invalid QR request.');
      case 401:
        throw SessionApiException(
            401, 'Authentication required. Please sign in.');
      case 403:
        throw SessionApiException(
            403, 'Permission denied for this parking operation.');
      case 404:
        throw SessionApiException(404,
            errorMsg.isNotEmpty ? errorMsg : 'Booking or session not found.');
      case 409:
        throw SessionApiException(
            409,
            errorMsg.isNotEmpty
                ? errorMsg
                : 'Conflicting parking state: session already active.');
      default:
        throw SessionApiException(response.statusCode, errorMsg);
    }
  }
}
