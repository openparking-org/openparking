import 'dart:io' show Platform;
import 'package:flutter/foundation.dart' show kIsWeb;

class ApiConfig {
  static String? _customBaseUrl;
  static String? _authToken;

  /// Returns the configured backend base URL, automatically resolving Android emulator localhost.
  static String get baseUrl {
    if (_customBaseUrl != null && _customBaseUrl!.isNotEmpty) {
      return _customBaseUrl!;
    }
    if (kIsWeb) {
      return 'http://localhost:5000';
    }
    try {
      if (Platform.isAndroid) {
        return 'http://10.0.2.2:5000';
      }
    } catch (_) {
      // Platform check fallback
    }
    return 'http://localhost:5000';
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
