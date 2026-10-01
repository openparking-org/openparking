class RoutePoint {
  final double x;
  final double y;

  RoutePoint({required this.x, required this.y});

  factory RoutePoint.fromJson(dynamic json) {
    if (json is List && json.length >= 2) {
      return RoutePoint(
        x: (json[0] as num).toDouble(),
        y: (json[1] as num).toDouble(),
      );
    } else if (json is Map<String, dynamic>) {
      return RoutePoint(
        x: (json['x'] ?? json['latitude'] as num).toDouble(),
        y: (json['y'] ?? json['longitude'] as num).toDouble(),
      );
    }
    return RoutePoint(x: 0, y: 0);
  }
}

class NavigationRouteModel {
  final List<RoutePoint> points;
  final double distanceMeters;
  final int estimatedSeconds;
  final List<String> instructions;

  NavigationRouteModel({
    required this.points,
    required this.distanceMeters,
    required this.estimatedSeconds,
    this.instructions = const [],
  });

  factory NavigationRouteModel.fromJson(Map<String, dynamic> json) {
    final rawPoints = json['points'] as List<dynamic>? ?? json['coordinates'] as List<dynamic>? ?? [];
    final rawInstructions = json['instructions'] as List<dynamic>? ?? [];

    return NavigationRouteModel(
      points: rawPoints.map((p) => RoutePoint.fromJson(p)).toList(),
      distanceMeters: (json['distanceMeters'] as num?)?.toDouble() ?? (json['distance'] as num?)?.toDouble() ?? 0.0,
      estimatedSeconds: json['estimatedSeconds'] as int? ?? json['duration'] as int? ?? 0,
      instructions: rawInstructions.map((i) => i.toString()).toList(),
    );
  }
}
