import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;
import 'dart:convert';
import 'package:flutter_dotenv/flutter_dotenv.dart';
import '../services/api_config.dart';

const secureStorage = FlutterSecureStorage();

class AuthState {
  final bool isAuthenticated;
  final String? token;
  final bool isLoading;

  AuthState({this.isAuthenticated = false, this.token, this.isLoading = true});
}

class AuthNotifier extends StateNotifier<AuthState> {
  AuthNotifier() : super(AuthState(isLoading: true)) {
    _init();
  }

  Future<void> _init() async {
    final token = await secureStorage.read(key: 'jwt_token');
    if (token != null) {
      ApiConfig.setAuthToken(token);
      state = AuthState(isAuthenticated: true, token: token, isLoading: false);
    } else {
      state = AuthState(isAuthenticated: false, isLoading: false);
    }
  }

  Future<bool> login(String email, String password) async {
    try {
      final baseUrl = dotenv.env['API_BASE_URL'] ?? 'http://localhost:5000';
      final response = await http.post(
        Uri.parse('$baseUrl/api/users/login'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'email': email, 'password': password}),
      );

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        final token = data['data']['token'];
        
        await secureStorage.write(key: 'jwt_token', value: token);
        ApiConfig.setAuthToken(token);
        state = AuthState(isAuthenticated: true, token: token, isLoading: false);
        return true;
      }
    } catch (e) {
      // Ignore
    }
    return false;
  }

  Future<void> logout() async {
    await secureStorage.delete(key: 'jwt_token');
    ApiConfig.setAuthToken(null);
    state = AuthState(isAuthenticated: false, isLoading: false);
  }
}

final authProvider = StateNotifierProvider<AuthNotifier, AuthState>((ref) {
  return AuthNotifier();
});
