import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/booking.dart';
import '../models/paginated_response.dart';
import 'api_config.dart';
import 'api_response.dart';

class BookingService {
  final http.Client _client;

  BookingService({http.Client? client})
      : _client = client ?? ParkingHttpClient();

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
      'startTime': startTime.toUtc().toIso8601String(),
      'endTime': endTime.toUtc().toIso8601String(),
      if (vehiclePlate != null && vehiclePlate.isNotEmpty)
        'vehiclePlate': vehiclePlate,
    };

    final response = await _client.post(
      url,
      headers: ApiConfig.headers,
      body: jsonEncode(payload),
    );

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final data = responseData(response);
      return BookingModel.fromJson(data as Map<String, dynamic>);
    }

    responseData(response);
    throw Exception("Could not create reservation.");
  }

  /// GET /api/bookings/{id}
  Future<BookingModel?> getBooking(String bookingId) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/bookings/$bookingId');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final data = responseData(response);
      return BookingModel.fromJson(data as Map<String, dynamic>);
    }
    responseData(response);
    return null;
  }

  /// GET /api/bookings/user?page=1&pageSize=10
  Future<PaginatedResponse<BookingModel>> getUserBookings({
    int page = 1,
    int pageSize = 10,
  }) async {
    final url = Uri.parse(
        '${ApiConfig.baseUrl}/api/customer/bookings?page=$page&pageSize=$pageSize');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final data = responseData(response);
      if (data is Map<String, dynamic> && data.containsKey('items')) {
        return PaginatedResponse<BookingModel>.fromJson(
          data,
          (itemJson) => BookingModel.fromJson(itemJson),
        );
      } else if (data is List) {
        final items = data
            .map((b) => BookingModel.fromJson(b as Map<String, dynamic>))
            .toList();
        return PaginatedResponse<BookingModel>(
          items: items,
          totalCount: items.length,
          page: 1,
          pageSize: items.length,
        );
      }
    }
    responseData(response);
    return PaginatedResponse<BookingModel>(
      items: [],
      totalCount: 0,
      page: page,
      pageSize: pageSize,
    );
  }

  /// POST /api/bookings/{id}/cancel
  Future<bool> cancelBooking(String bookingId) async {
    final url =
        Uri.parse('${ApiConfig.baseUrl}/api/bookings/$bookingId/cancel');
    final response = await _client.post(url, headers: ApiConfig.headers);
    responseData(response);
    return true;
  }
}
