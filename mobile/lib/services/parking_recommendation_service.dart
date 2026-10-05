import 'dart:convert';
import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:http/http.dart' as http;
import 'api_config.dart';
import 'api_response.dart';

class ParkingLocation {
  final double latitude, longitude, accuracy;
  const ParkingLocation(this.latitude, this.longitude, this.accuracy);
}

class ParkingLocationService {
  Future<ParkingLocation> current() async {
    try {
      return await _current();
    } on TimeoutException {
      throw Exception(
          'Could not get your location. Check device location or set an emulator GPS location, then retry.');
    }
  }

  Future<ParkingLocation> _current() async {
    if (!await Geolocator.isLocationServiceEnabled()
        .timeout(const Duration(seconds: 8))) {
      throw Exception('Turn on device location, then try again.');
    }
    var permission =
        await Geolocator.checkPermission().timeout(const Duration(seconds: 8));
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }
    if (permission == LocationPermission.deniedForever) {
      throw Exception(
          'Allow location for OpenParking in your device settings.');
    }
    if (permission == LocationPermission.denied) {
      throw Exception(
          'Location permission is needed for nearby recommendations. You can still browse zones below.');
    }
    final settings = defaultTargetPlatform == TargetPlatform.android
        ? AndroidSettings(
            forceLocationManager: true,
            accuracy: LocationAccuracy.high,
            timeLimit: const Duration(seconds: 20))
        : const LocationSettings(
            accuracy: LocationAccuracy.high, timeLimit: Duration(seconds: 20));
    final position =
        await Geolocator.getCurrentPosition(locationSettings: settings)
            .timeout(const Duration(seconds: 22));
    return ParkingLocation(
        position.latitude, position.longitude, position.accuracy);
  }
}

class ParkingRecommendation {
  final String zoneId, name, currency, reason;
  final double distanceMeters, hourlyRate;
  final int availableCount;
  ParkingRecommendation.fromJson(Map<String, dynamic> json)
      : zoneId = json['zoneId'] as String,
        name = json['name'] as String,
        currency = json['currency'] as String,
        reason = json['reason'] as String,
        distanceMeters = (json['distanceMeters'] as num).toDouble(),
        hourlyRate = (json['hourlyRate'] as num).toDouble(),
        availableCount = json['availableCount'] as int;
}

class ParkingRecommendations {
  final String requestId, message;
  final List<ParkingRecommendation> items;
  ParkingRecommendations.fromJson(Map<String, dynamic> json)
      : requestId = json['requestId'] as String,
        message = json['message'] as String,
        items = (json['recommendations'] as List)
            .map((r) =>
                ParkingRecommendation.fromJson(r as Map<String, dynamic>))
            .toList();
}

class ParkingRecommendationService {
  final http.Client _client;
  ParkingRecommendationService({http.Client? client})
      : _client = client ?? ParkingHttpClient();
  Future<ParkingRecommendations> find(ParkingLocation location,
      {String preference = 'nearest',
      double radiusKm = 25,
      String slotType = 'Standard'}) async {
    final response = await _client.post(
      Uri.parse('${ApiConfig.baseUrl}/api/parking/recommendations'),
      headers: ApiConfig.headers,
      body: jsonEncode({
        'latitude': location.latitude,
        'longitude': location.longitude,
        'preference': preference,
        'radiusKm': radiusKm,
        'slotType': slotType
      }),
    );
    return ParkingRecommendations.fromJson(
        responseData(response) as Map<String, dynamic>);
  }
}

final parkingLocationProvider =
    Provider<ParkingLocationService>((ref) => ParkingLocationService());
final parkingRecommendationProvider = Provider<ParkingRecommendationService>(
    (ref) => ParkingRecommendationService());
