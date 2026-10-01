class SlotModel {
  final String id;
  final String slotNumber;
  final String type; // Standard, EVCharging, Disability, Reserved, Compact
  final String status; // Available, Occupied, Reserved, Maintenance, OutOfService
  final int floor;
  final String? boundingBoxJson;
  final String? assignedSensorId;
  final String? assignedCameraId;

  SlotModel({
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
