import 'slot.dart';
import 'floor_plan.dart';

class ZoneModel {
  final String id;
  final String name;
  final String code;
  final double latitude;
  final double longitude;
  final double baseHourlyRate;
  final int totalCapacity;
  final int availableCount;
  final String currency;
  final List<SlotModel> slots;
  final List<FloorPlanModel> floorPlans;

  ZoneModel({
    required this.id,
    required this.name,
    required this.code,
    required this.latitude,
    required this.longitude,
    required this.baseHourlyRate,
    required this.totalCapacity,
    required this.availableCount,
    required this.currency,
    this.slots = const [],
    this.floorPlans = const [],
  });

  factory ZoneModel.fromJson(Map<String, dynamic> json) {
    final rawSlots = json['slots'] as List<dynamic>? ?? [];
    final rawFloorPlans = json['floorPlans'] as List<dynamic>? ?? [];

    return ZoneModel(
      id: json['id']?.toString() ?? '',
      name: json['name']?.toString() ?? '',
      code: json['code']?.toString() ?? '',
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0.0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0.0,
      baseHourlyRate: (json['baseHourlyRate'] as num?)?.toDouble() ?? 0.0,
      totalCapacity: json['totalCapacity'] as int? ?? json['capacity'] as int? ?? 0,
      availableCount: json['availableCount'] as int? ?? json['availableSlots'] as int? ?? 0,
      currency: json['currency']?.toString() ?? 'USD',
      slots: rawSlots.map((s) => SlotModel.fromJson(s as Map<String, dynamic>)).toList(),
      floorPlans: rawFloorPlans.map((f) => FloorPlanModel.fromJson(f as Map<String, dynamic>)).toList(),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'name': name,
        'code': code,
        'latitude': latitude,
        'longitude': longitude,
        'baseHourlyRate': baseHourlyRate,
        'totalCapacity': totalCapacity,
        'availableCount': availableCount,
        'currency': currency,
        'slots': slots.map((s) => s.toJson()).toList(),
        'floorPlans': floorPlans.map((f) => f.toJson()).toList(),
      };
}
