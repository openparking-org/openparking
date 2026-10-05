import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/user.dart';
import 'api_config.dart';
import 'api_response.dart';

class UserService {
  final http.Client _client;

  UserService({http.Client? client}) : _client = client ?? ParkingHttpClient();

  /// GET /api/users/me
  Future<UserModel?> getProfile() async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/users/me');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final data = responseData(response);
      return UserModel.fromJson(data as Map<String, dynamic>);
    }
    return null;
  }

  /// POST /api/users/register
  Future<Map<String, dynamic>> register({
    required String fullName,
    required String email,
    required String password,
  }) async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/users/register');
    final payload = {
      'fullName': fullName,
      'email': email,
      'password': password,
    };

    final response = await _client.post(
      url,
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode(payload),
    );

    if (response.statusCode >= 200 && response.statusCode < 300) {
      return responseData(response) as Map<String, dynamic>;
    }

    responseData(response);
    throw Exception("Registration failed.");
  }
}
