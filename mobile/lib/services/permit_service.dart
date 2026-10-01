import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/permit.dart';
import 'api_config.dart';

class PermitService {
  final http.Client _client;

  PermitService({http.Client? client}) : _client = client ?? http.Client();

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
      'issuingAuthority': issuingAuthority,
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
      final json = jsonDecode(response.body);
      final data = json['data'] ?? json;
      return DisabilityPermitModel.fromJson(data as Map<String, dynamic>);
    }

    String errorMsg = 'Failed to submit disability permit';
    try {
      final err = jsonDecode(response.body);
      if (err is Map && err['message'] != null) errorMsg = err['message'];
    } catch (_) {}
    throw Exception(errorMsg);
  }

  /// GET /api/users/me/permit
  Future<DisabilityPermitModel?> getMyPermit() async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/users/me/permit');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final json = jsonDecode(response.body);
      final data = json['data'] ?? json;
      return DisabilityPermitModel.fromJson(data as Map<String, dynamic>);
    }
    return null;
  }
}
