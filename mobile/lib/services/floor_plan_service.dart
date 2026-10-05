import 'package:http/http.dart' as http;
import '../models/floor_plan.dart';
import '../models/route.dart';
import 'api_config.dart';
import 'api_response.dart';

class FloorPlanService {
  final http.Client _client;
  FloorPlanService({http.Client? client})
      : _client = client ?? ParkingHttpClient();
  Future<List<FloorPlanModel>> getFloorPlansForZone(String zoneId) async {
    final response = await _client
        .get(Uri.parse('${ApiConfig.baseUrl}/api/zones/$zoneId/floor-plans'),
            headers: ApiConfig.headers)
        .timeout(const Duration(seconds: 15));
    final data = responseData(response) as List;
    return data
        .map((f) => FloorPlanModel.fromJson(f as Map<String, dynamic>))
        .toList();
  }

  Future<FloorPlanModel?> getFloorPlan(String id) async {
    final response = await _client
        .get(Uri.parse('${ApiConfig.baseUrl}/api/zones/floor-plans/$id'),
            headers: ApiConfig.headers)
        .timeout(const Duration(seconds: 15));
    if (response.statusCode == 404) return null;
    return FloorPlanModel.fromJson(
        responseData(response) as Map<String, dynamic>);
  }

  Future<NavigationRouteModel?> getNavigationRoute(
      {required String zoneId,
      required String targetSlotId,
      String? bookingId}) async {
    final url = bookingId != null && bookingId.isNotEmpty
        ? Uri.parse('${ApiConfig.baseUrl}/api/bookings/$bookingId/route')
        : Uri.parse('${ApiConfig.baseUrl}/api/zones/$zoneId/route')
            .replace(queryParameters: {'targetSlotId': targetSlotId});
    final response = await _client
        .get(url, headers: ApiConfig.headers)
        .timeout(const Duration(seconds: 15));
    return NavigationRouteModel.fromJson(
        responseData(response) as Map<String, dynamic>);
  }
}
