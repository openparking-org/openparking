import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/booking.dart';
import '../models/paginated_response.dart';
import 'api_config.dart';

class BookingService {
  final http.Client _client;

  BookingService({http.Client? client}) : _client = client ?? http.Client();

  /// POST /api/bookings
  Future<BookingModel> createBooking({
    required String slotId,
    required DateTime startTime,
    required DateTime endTime,
    String? vehiclePlate,
  }) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/bookings');
    final payload = {
      'slotId': slotId,
      // UTC with a trailing Z. A local DateTime's toIso8601String() carries no
      // offset, which the API cannot place in time and used to reject with 500.
      'startTime': startTime.toUtc().toIso8601String(),
      'endTime': endTime.toUtc().toIso8601String(),
      if (vehiclePlate != null && vehiclePlate.isNotEmpty) 'vehiclePlate': vehiclePlate,
    };

    final response = await _client.post(
      url,
      headers: ApiConfig.headers,
      body: jsonEncode(payload),
    );

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final json = jsonDecode(response.body);
      final data = json['data'] ?? json;
      return BookingModel.fromJson(data as Map<String, dynamic>);
    }

    String errorMsg = 'Failed to create booking';
    try {
      final err = jsonDecode(response.body);
      if (err is Map && err['message'] != null) errorMsg = err['message'];
    } catch (_) {}
    throw Exception(errorMsg);
  }

  /// GET /api/bookings/{id}
  Future<BookingModel?> getBooking(String bookingId) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/bookings/$bookingId');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final json = jsonDecode(response.body);
      final data = json['data'] ?? json;
      return BookingModel.fromJson(data as Map<String, dynamic>);
    }
    return null;
  }

  /// GET /api/bookings/user?page=1&pageSize=10
  Future<PaginatedResponse<BookingModel>> getUserBookings({
    int page = 1,
    int pageSize = 10,
  }) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/bookings/user?page=$page&pageSize=$pageSize');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final json = jsonDecode(response.body);
      final data = json['data'] ?? json;
      if (data is Map<String, dynamic> && data.containsKey('items')) {
        return PaginatedResponse<BookingModel>.fromJson(
          data,
          (itemJson) => BookingModel.fromJson(itemJson),
        );
      } else if (data is List) {
        final items = data.map((b) => BookingModel.fromJson(b as Map<String, dynamic>)).toList();
        return PaginatedResponse<BookingModel>(
          items: items,
          totalCount: items.length,
          page: 1,
          pageSize: items.length,
        );
      }
    }
    return PaginatedResponse<BookingModel>(
      items: [],
      totalCount: 0,
      page: page,
      pageSize: pageSize,
    );
  }

  /// POST /api/bookings/{id}/cancel
  Future<bool> cancelBooking(String bookingId) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/bookings/$bookingId/cancel');
    final response = await _client.post(url, headers: ApiConfig.headers);
    return response.statusCode == 200;
  }
}
