import 'package:http/http.dart' as http;
import '../models/zone.dart';
import '../models/slot.dart';
import '../models/paginated_response.dart';
import 'api_config.dart';
import 'api_response.dart';

class ZoneService {
  final http.Client _client;

  ZoneService({http.Client? client}) : _client = client ?? ParkingHttpClient();

  /// GET /api/zones?page=1&pageSize=10&search=...
  Future<PaginatedResponse<ZoneModel>> getZones({
    int page = 1,
    int pageSize = 10,
    String? search,
    String? filter,
  }) async {
    final queryParams = <String, String>{
      'page': page.toString(),
      'pageSize': pageSize.toString(),
      if (filter != null) 'filter': filter,
    };
    if (search != null && search.trim().isNotEmpty) {
      queryParams['search'] = search.trim();
    }

    final url = Uri.parse('${ApiConfig.baseUrl}/api/zones')
        .replace(queryParameters: queryParams);

    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final data = responseData(response);
      if (data is Map<String, dynamic> && data.containsKey('items')) {
        return PaginatedResponse<ZoneModel>.fromJson(
          data,
          (itemJson) => ZoneModel.fromJson(itemJson),
        );
      } else if (data is List) {
        final items = data
            .map((z) => ZoneModel.fromJson(z as Map<String, dynamic>))
            .toList();
        return PaginatedResponse<ZoneModel>(
          items: items,
          totalCount: items.length,
          page: 1,
          pageSize: items.length,
        );
      }
    }
    responseData(response);
    return PaginatedResponse<ZoneModel>(
      items: [],
      totalCount: 0,
      page: page,
      pageSize: pageSize,
    );
  }

  /// GET /api/zones/{id}
  Future<ZoneModel?> getZoneDetail(String zoneId) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/zones/$zoneId');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final data = responseData(response);
      if (data == null) return null;
      return ZoneModel.fromJson(data as Map<String, dynamic>);
    }
    responseData(response);
    return null;
  }

  /// GET /api/zones/{id}/slots/available
  Future<List<SlotModel>> getAvailableSlots(String zoneId,
      {String? type}) async {
    final queryParams = <String, String>{};
    if (type != null) queryParams['type'] = type;

    final url =
        Uri.parse('${ApiConfig.baseUrl}/api/zones/$zoneId/slots/available')
            .replace(queryParameters: queryParams);
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final data = responseData(response) as List<dynamic>;
      return data
          .map((s) => SlotModel.fromJson(s as Map<String, dynamic>))
          .toList();
    }
    responseData(response);
    return [];
  }
}
