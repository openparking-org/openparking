class SlotModel {
  final String? floorPlanId;
  final double? canvasX, canvasY, canvasWidth, canvasHeight;
  final String id;
  final String slotNumber;
  final String type; // Standard, EVCharging, Disability, Reserved, Compact
  final String
      status; // Available, Occupied, Reserved, Maintenance, OutOfService
  final int floor;
  final String? boundingBoxJson;
  final String? assignedSensorId;
  final String? assignedCameraId;

  SlotModel({
    this.floorPlanId,
    this.canvasX,
    this.canvasY,
    this.canvasWidth,
    this.canvasHeight,
    required this.id,
    required this.slotNumber,
    required this.type,
    required this.status,
    required this.floor,
    this.boundingBoxJson,
    this.assignedSensorId,
    this.assignedCameraId,
  });

  bool get isAvailable => status.toLowerCase() == 'available';

  factory SlotModel.fromJson(Map<String, dynamic> json) {
    return SlotModel(
      floorPlanId: json['floorPlanId']?.toString(),
      canvasX: (json['canvasX'] as num?)?.toDouble(),
      canvasY: (json['canvasY'] as num?)?.toDouble(),
      canvasWidth: (json['canvasWidth'] as num?)?.toDouble(),
      canvasHeight: (json['canvasHeight'] as num?)?.toDouble(),
      id: json['id']?.toString() ?? '',
      slotNumber: json['slotNumber']?.toString() ?? '',
      type: json['type']?.toString() ?? 'Standard',
      status: json['status']?.toString() ?? 'Available',
      floor: json['floor'] as int? ?? 1,
      boundingBoxJson: json['boundingBoxJson']?.toString(),
      assignedSensorId: json['assignedSensorId']?.toString(),
      assignedCameraId: json['assignedCameraId']?.toString(),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'slotNumber': slotNumber,
        'type': type,
        'status': status,
        'floor': floor,
        'boundingBoxJson': boundingBoxJson,
        'assignedSensorId': assignedSensorId,
        'assignedCameraId': assignedCameraId,
      };
}
