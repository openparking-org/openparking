import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/permit.dart';
import 'api_config.dart';
import 'api_response.dart';

class PermitService {
  final http.Client _client;

  PermitService({http.Client? client})
      : _client = client ?? ParkingHttpClient();

  /// POST /api/users/{userId}/permits
  Future<DisabilityPermitModel> submitPermit({
    required String userId,
    required String permitNumber,
    required String issuingAuthority,
    required DateTime expiryDate,
    String? documentBase64,
    String? documentImageUrl,
  }) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/users/$userId/permits');
    final payload = {
      'permitNumber': permitNumber,
      'jurisdiction': issuingAuthority,
      'expiryDate': expiryDate.toIso8601String(),
      if (documentBase64 != null) 'documentBase64': documentBase64,
      if (documentImageUrl != null) 'documentImageUrl': documentImageUrl,
    };

    final response = await _client.post(
      url,
      headers: ApiConfig.headers,
      body: jsonEncode(payload),
    );

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final data = responseData(response);
      return DisabilityPermitModel.fromJson(data as Map<String, dynamic>);
    }

    responseData(response);
    throw Exception("Could not submit permit.");
  }

  /// GET /api/users/me/permit
  Future<DisabilityPermitModel?> getMyPermit() async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/users/me/permit');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final data = responseData(response);
      return DisabilityPermitModel.fromJson(data as Map<String, dynamic>);
    }
    if (response.statusCode != 404) responseData(response);
    return null;
  }
}
