class FloorPlanModel {
  final double imageWidthPx, imageHeightPx;
  final String id;
  final String floorName;
  final int floorOrder;
  final String imageUrl;
  final double? anchorNorthWestLat;
  final double? anchorNorthWestLng;
  final double? anchorSouthEastLat;
  final double? anchorSouthEastLng;

  FloorPlanModel({
    this.imageWidthPx = 1000,
    this.imageHeightPx = 600,
    required this.id,
    required this.floorName,
    required this.floorOrder,
    required this.imageUrl,
    this.anchorNorthWestLat,
    this.anchorNorthWestLng,
    this.anchorSouthEastLat,
    this.anchorSouthEastLng,
  });

  factory FloorPlanModel.fromJson(Map<String, dynamic> json) {
    return FloorPlanModel(
      imageWidthPx: (json['imageWidthPx'] as num?)?.toDouble() ?? 1000,
      imageHeightPx: (json['imageHeightPx'] as num?)?.toDouble() ?? 600,
      id: json['id']?.toString() ?? '',
      floorName: json['floorName']?.toString() ?? '',
      floorOrder: json['floorOrder'] as int? ?? 1,
      imageUrl: json['imageUrl']?.toString() ?? '',
      anchorNorthWestLat: (json['anchorNorthWestLat'] as num?)?.toDouble(),
      anchorNorthWestLng: (json['anchorNorthWestLng'] as num?)?.toDouble(),
      anchorSouthEastLat: (json['anchorSouthEastLat'] as num?)?.toDouble(),
      anchorSouthEastLng: (json['anchorSouthEastLng'] as num?)?.toDouble(),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'floorName': floorName,
        'floorOrder': floorOrder,
        'imageUrl': imageUrl,
        'anchorNorthWestLat': anchorNorthWestLat,
        'anchorNorthWestLng': anchorNorthWestLng,
        'anchorSouthEastLat': anchorSouthEastLat,
        'anchorSouthEastLng': anchorSouthEastLng,
      };
}
