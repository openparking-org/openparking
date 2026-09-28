import 'dart:convert';
import 'package:http/http.dart' as http;
import 'api_config.dart';

class ZoneModel {
  final String id;
  final String name;
  final int capacity;
  final int availableSlots;
  final double currentPriceMultiplier;

  ZoneModel({
    required this.id,
    required this.name,
    required this.capacity,
    required this.availableSlots,
    required this.currentPriceMultiplier,
  });

  factory ZoneModel.fromJson(Map<String, dynamic> json) {
    return ZoneModel(
      id: json['id']?.toString() ?? '',
      name: json['name']?.toString() ?? '',
      capacity: json['capacity'] as int? ?? 0,
      availableSlots: json['availableSlots'] as int? ?? 0,
      currentPriceMultiplier:
          (json['currentPriceMultiplier'] as num?)?.toDouble() ?? 1.0,
    );
  }
}

class ZoneService {
  final http.Client _client = http.Client();

  Future<List<ZoneModel>> getZones() async {
    final url = Uri.parse('${ApiConfig.baseUrl}/api/zones');
    final response = await _client.get(url, headers: ApiConfig.headers);

    if (response.statusCode == 200) {
      final List<dynamic> data = jsonDecode(response.body);
      return data.map((json) => ZoneModel.fromJson(json)).toList();
    }
    return [];
  }
}
