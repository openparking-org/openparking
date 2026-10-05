import 'dart:convert';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;
import '../models/user.dart';
import '../services/api_config.dart';
import '../services/api_response.dart';

const secureStorage = FlutterSecureStorage();

class AuthState {
  final bool isAuthenticated;
  final String? token;
  final UserModel? user;
  final bool isLoading;
  final String? error;
  AuthState(
      {this.isAuthenticated = false,
      this.token,
      this.user,
      this.isLoading = true,
      this.error});
}

class AuthNotifier extends StateNotifier<AuthState> {
  AuthNotifier() : super(AuthState()) {
    ApiConfig.onUnauthorized = logout;
    _init();
  }
  Future<void> _init() async {
    try {
      final token = await secureStorage.read(key: 'jwt_token');
      if (token == null) {
        state = AuthState(isLoading: false);
        return;
      }
      ApiConfig.setAuthToken(token);
      final response = await http
          .get(Uri.parse('${ApiConfig.baseUrl}/api/users/me'),
              headers: ApiConfig.headers)
          .timeout(const Duration(seconds: 15));
      final user =
          UserModel.fromJson(responseData(response) as Map<String, dynamic>);
      if (mounted) {
        state = AuthState(
            isAuthenticated: true, token: token, user: user, isLoading: false);
      }
    } catch (_) {
      ApiConfig.setAuthToken(null);
      if (mounted) {
        state =
            AuthState(isLoading: false, error: 'Please sign in to continue.');
      }
    }
  }

  Future<bool> login(String email, String password,
      {bool remember = true}) async {
    try {
      final response = await http
          .post(Uri.parse('${ApiConfig.baseUrl}/api/users/login'),
              headers: {'Content-Type': 'application/json'},
              body: jsonEncode({'email': email, 'password': password}))
          .timeout(const Duration(seconds: 15));
      final data = responseData(response) as Map<String, dynamic>;
      final token = data['token'] as String;
      final user = UserModel.fromJson(data['user'] as Map<String, dynamic>);
      if (remember) {
        await secureStorage.write(key: 'jwt_token', value: token);
      } else {
        await secureStorage.delete(key: 'jwt_token');
      }
      ApiConfig.setAuthToken(token);
      state = AuthState(
          isAuthenticated: true, token: token, user: user, isLoading: false);
      return true;
    } catch (e) {
      state = AuthState(
          isLoading: false,
          error: e.toString().replaceFirst('Exception: ', ''));
      return false;
    }
  }

  Future<void> logout() async {
    ApiConfig.setAuthToken(null);
    if (mounted) state = AuthState(isLoading: false);
    await secureStorage.delete(key: 'jwt_token');
  }

  @override
  void dispose() {
    ApiConfig.onUnauthorized = null;
    super.dispose();
  }
}

final authProvider =
    StateNotifierProvider<AuthNotifier, AuthState>((ref) => AuthNotifier());
