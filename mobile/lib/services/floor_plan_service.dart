import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/floor_plan.dart';
import '../models/route.dart';
import 'api_config.dart';

class FloorPlanService {
  final http.Client _client;

  FloorPlanService({http.Client? client}) : _client = client ?? http.Client();

  /// GET /api/zones/{zoneId}/floor-plans
  Future<List<FloorPlanModel>> getFloorPlansForZone(String zoneId) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/zones/$zoneId/floor-plans');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final json = jsonDecode(response.body);
      final List<dynamic> data = json['data'] ?? json;
      return data.map((f) => FloorPlanModel.fromJson(f as Map<String, dynamic>)).toList();
    }
    return [];
  }

  /// GET /api/zones/floor-plans/{floorPlanId}
  Future<FloorPlanModel?> getFloorPlan(String floorPlanId) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/zones/floor-plans/$floorPlanId');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final json = jsonDecode(response.body);
      final data = json['data'] ?? json;
      return FloorPlanModel.fromJson(data as Map<String, dynamic>);
    }
    return null;
  }

  /// GET /api/bookings/{bookingId}/route or fallback route generation
  Future<NavigationRouteModel?> getNavigationRoute({
    required String zoneId,
    required String targetSlotId,
    String? bookingId,
  }) async {
    if (bookingId != null && bookingId.isNotEmpty) {
      final url = Uri.parse('${ApiConfig.baseUrl}/api/bookings/$bookingId/route');
      try {
        final response = await _client.get(url, headers: ApiConfig.headers);
        if (response.statusCode == 200) {
          final json = jsonDecode(response.body);
          final data = json['data'] ?? json;
          return NavigationRouteModel.fromJson(data as Map<String, dynamic>);
        }
      } catch (_) {}
    }

    // Try zone route endpoint fallback
    final url = Uri.parse('${ApiConfig.baseUrl}/api/zones/$zoneId/route?targetSlotId=$targetSlotId');
    try {
      final response = await _client.get(url, headers: ApiConfig.headers);
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        final data = json['data'] ?? json;
        return NavigationRouteModel.fromJson(data as Map<String, dynamic>);
      }
    } catch (_) {}

    return null;
  }
}
