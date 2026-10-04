import 'dart:convert';
import 'dart:io' show SocketException;
import 'package:http/http.dart' as http;
import 'api_config.dart';
import 'api_response.dart';

class SessionApiException implements Exception {
  final int statusCode;
  final String message;

  SessionApiException(this.statusCode, this.message);

  @override
  String toString() => message;
}

class ParkingSessionModel {
  final String currency;
  final String vehiclePlate;
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
    this.currency = 'USD',
    this.vehiclePlate = '',
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
      currency: json['currency']?.toString() ?? 'USD',
      vehiclePlate: json['vehiclePlate']?.toString() ?? '',
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

  SessionService({http.Client? client})
      : _client = client ?? ParkingHttpClient();

  /// Fetches the currently active session for driver tracker screen.
  Future<ParkingSessionModel?> getActiveSession({
    String? userId,
    String? bookingId,
  }) async {
    final queryParams = <String, String>{};
    if (userId != null && userId.isNotEmpty) queryParams['userId'] = userId;
    if (bookingId != null && bookingId.isNotEmpty) {
      queryParams['bookingId'] = bookingId;
    }

    final baseUri = Uri.parse('${ApiConfig.baseUrl}/api/sessions/active');
    final url = baseUri.replace(
        queryParameters: queryParams.isNotEmpty ? queryParams : null);

    try {
      final response = await _client.get(url, headers: ApiConfig.headers);
      if (response.statusCode == 404) {
        return null;
      }
      return _handleResponse(response);
    } on SocketException {
      throw SessionApiException(0, "Unable to connect. Please try again.");
    }
  }

  ParkingSessionModel _handleResponse(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      final json = jsonDecode(response.body);
      final data = (json is Map<String, dynamic> && json.containsKey('data'))
          ? json['data']
          : json;
      if (data is Map<String, dynamic>) {
        return ParkingSessionModel.fromJson(data);
      }
      throw SessionApiException(
          response.statusCode, 'Unexpected server response format.');
    }

    try {
      responseData(response);
    } catch (e) {
      throw SessionApiException(
          response.statusCode, e.toString().replaceFirst("Exception: ", ""));
    }
    throw SessionApiException(
        response.statusCode, "Unexpected session response.");
  }
}
