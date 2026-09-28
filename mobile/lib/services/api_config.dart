import 'dart:io' show Platform;
import 'package:flutter/foundation.dart' show kIsWeb;

import 'package:flutter_dotenv/flutter_dotenv.dart';

class ApiConfig {
  static String? _customBaseUrl;
  static String? _authToken;

  /// Returns the configured backend base URL from .env, resolving Android emulator localhost.
  static String get baseUrl {
    if (_customBaseUrl != null && _customBaseUrl!.isNotEmpty) {
      return _customBaseUrl!;
    }

    String envUrl = dotenv.env['API_BASE_URL'] ?? 'http://localhost:5000';

    // Auto-resolve localhost for Android emulator
    if (!kIsWeb && envUrl.contains('localhost')) {
      try {
        if (Platform.isAndroid) {
          return envUrl.replaceAll('localhost', '10.0.2.2');
        }
      } catch (_) {}
    }
    return envUrl;
  }

  static void setBaseUrl(String url) {
    _customBaseUrl = url.replaceAll(RegExp(r'/+$'), '');
  }

  static String? get authToken => _authToken;

  static void setAuthToken(String? token) {
    _authToken = token;
  }

  static Map<String, String> get headers {
    final headers = <String, String>{
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };
    if (_authToken != null && _authToken!.isNotEmpty) {
      headers['Authorization'] = 'Bearer $_authToken';
    }
    return headers;
  }
}
