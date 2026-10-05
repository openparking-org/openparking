import 'dart:convert';
import 'package:http/http.dart' as http;
import 'api_config.dart';

class ParkingHttpClient extends http.BaseClient {
  final http.Client _inner = http.Client();
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) =>
      _inner.send(request).timeout(const Duration(seconds: 15),
          onTimeout: () => throw Exception(
              'The server took too long to respond. Please try again.'));
  @override
  void close() => _inner.close();
}

dynamic responseData(http.Response response) {
  dynamic payload;
  try {
    payload = jsonDecode(response.body);
  } catch (_) {}
  if (response.statusCode == 401) {
    final requestToken = response.request?.headers['Authorization'] ??
        response.request?.headers['authorization'];
    if (requestToken == null ||
        requestToken == 'Bearer ${ApiConfig.authToken}') {
      ApiConfig.onUnauthorized?.call();
    }
  }
  if (response.statusCode < 200 ||
      response.statusCode >= 300 ||
      payload is Map && payload['success'] == false) {
    final error = payload is Map ? payload['error'] : null;
    final message = error is Map
        ? error['message']
        : payload is Map
            ? payload['message'] ?? payload['title']
            : null;
    throw Exception(message ??
        'Request failed (${response.statusCode}). Please try again.');
  }
  return payload is Map && payload.containsKey('data')
      ? payload['data']
      : payload;
}
