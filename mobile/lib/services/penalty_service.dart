import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/penalty.dart';
import '../models/paginated_response.dart';
import 'api_config.dart';

class PenaltyService {
  final http.Client _client;

  PenaltyService({http.Client? client}) : _client = client ?? http.Client();

  /// GET /api/penalties/my?page=1&pageSize=10
  Future<PaginatedResponse<PenaltyModel>> getMyPenalties({
    int page = 1,
    int pageSize = 10,
  }) async {
    Uri url = Uri.parse('${ApiConfig.baseUrl}/api/penalties/my?page=$page&pageSize=$pageSize');
    http.Response response;

    try {
      response = await _client.get(url, headers: ApiConfig.headers);
      if (response.statusCode == 404) {
        // Fallback endpoint if /api/penalties/my is route-mapped differently
        url = Uri.parse('${ApiConfig.baseUrl}/api/users/me/penalties?page=$page&pageSize=$pageSize');
        response = await _client.get(url, headers: ApiConfig.headers);
      }
    } catch (_) {
      return PaginatedResponse<PenaltyModel>(
        items: [],
        totalCount: 0,
        page: page,
        pageSize: pageSize,
      );
    }

    if (response.statusCode == 200) {
      final json = jsonDecode(response.body);
      final data = json['data'] ?? json;
      if (data is Map<String, dynamic> && data.containsKey('items')) {
        return PaginatedResponse<PenaltyModel>.fromJson(
          data,
          (itemJson) => PenaltyModel.fromJson(itemJson),
        );
      } else if (data is List) {
        final items = data.map((p) => PenaltyModel.fromJson(p as Map<String, dynamic>)).toList();
        return PaginatedResponse<PenaltyModel>(
          items: items,
          totalCount: items.length,
          page: 1,
          pageSize: items.length,
        );
      }
    }

    return PaginatedResponse<PenaltyModel>(
      items: [],
      totalCount: 0,
      page: page,
      pageSize: pageSize,
    );
  }

  /// POST /api/penalties/{id}/dispute
  Future<bool> disputePenalty(String penaltyId, String reason) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/penalties/$penaltyId/dispute');
    final response = await _client.post(
      url,
      headers: ApiConfig.headers,
      body: jsonEncode({'reason': reason, 'disputeNotes': reason}),
    );

    return response.statusCode >= 200 && response.statusCode < 300;
  }
}
