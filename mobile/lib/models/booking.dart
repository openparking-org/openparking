import 'slot.dart';

class BookingModel {
  final String id;
  final String userId;
  final String slotId;
  final DateTime startTime;
  final DateTime endTime;
  final String? vehiclePlate;
  final String status; // Pending, Active, Completed, Cancelled
  final String qrCodeContent;
  final double estimatedFee;
  final SlotModel? slot;

  BookingModel({
    required this.id,
    required this.userId,
    required this.slotId,
    required this.startTime,
    required this.endTime,
    this.vehiclePlate,
    required this.status,
    required this.qrCodeContent,
    required this.estimatedFee,
    this.slot,
  });

  factory BookingModel.fromJson(Map<String, dynamic> json) {
    return BookingModel(
      id: json['id']?.toString() ?? '',
      userId: json['userId']?.toString() ?? '',
      slotId: json['slotId']?.toString() ?? '',
      startTime: json['startTime'] != null
          ? DateTime.parse(json['startTime'].toString())
          : DateTime.now(),
      endTime: json['endTime'] != null
          ? DateTime.parse(json['endTime'].toString())
          : DateTime.now().add(const Duration(hours: 2)),
      vehiclePlate: json['vehiclePlate']?.toString(),
      status: json['status']?.toString() ?? 'Pending',
      qrCodeContent: json['qrCodeContent']?.toString() ?? '',
      estimatedFee: (json['estimatedFee'] as num?)?.toDouble() ?? 0.0,
      slot: json['slot'] != null
          ? SlotModel.fromJson(json['slot'] as Map<String, dynamic>)
          : null,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'userId': userId,
        'slotId': slotId,
        'startTime': startTime.toIso8601String(),
        'endTime': endTime.toIso8601String(),
        'vehiclePlate': vehiclePlate,
        'status': status,
        'qrCodeContent': qrCodeContent,
        'estimatedFee': estimatedFee,
        if (slot != null) 'slot': slot!.toJson(),
      };
}

class ParkingSessionModel {
  final String id;
  final String bookingId;
  final String userId;
  final String slotId;
  final String slotNumber;
  final String zoneName;
  final String? zoneId;
  final DateTime checkInTime;
  final DateTime? checkOutTime;
  final String status; // Active, Completed, OverstayDetected, Terminated
  final int overstayMinutes;
  final double totalFee;
  final double penaltyFee;
  final String? receiptPdfUrl;
  final double hourlyRate;
  final String currency;

  ParkingSessionModel({
    required this.id,
    required this.bookingId,
    required this.userId,
    required this.slotId,
    required this.slotNumber,
    required this.zoneName,
    this.zoneId,
    required this.checkInTime,
    this.checkOutTime,
    required this.status,
    required this.overstayMinutes,
    required this.totalFee,
    required this.penaltyFee,
    this.receiptPdfUrl,
    required this.hourlyRate,
    required this.currency,
  });

  factory ParkingSessionModel.fromJson(Map<String, dynamic> json) {
    return ParkingSessionModel(
      id: json['id']?.toString() ?? '',
      bookingId: json['bookingId']?.toString() ?? '',
      userId: json['userId']?.toString() ?? '',
      slotId: json['slotId']?.toString() ?? '',
      slotNumber: json['slotNumber']?.toString() ?? 'Unassigned',
      zoneName: json['zoneName']?.toString() ?? 'Main Lot',
      zoneId: json['zoneId']?.toString(),
      checkInTime: json['checkInTime'] != null
          ? DateTime.parse(json['checkInTime'].toString())
          : DateTime.now(),
      checkOutTime: json['checkOutTime'] != null
          ? DateTime.parse(json['checkOutTime'].toString())
          : null,
      status: json['status']?.toString() ?? 'Active',
      overstayMinutes: json['overstayMinutes'] as int? ?? 0,
      totalFee: (json['totalFee'] as num?)?.toDouble() ?? 0.0,
      penaltyFee: (json['penaltyFee'] as num?)?.toDouble() ?? 0.0,
      receiptPdfUrl: json['receiptPdfUrl']?.toString(),
      hourlyRate: (json['hourlyRate'] as num?)?.toDouble() ?? 5.0,
      currency: json['currency']?.toString() ?? 'USD',
    );
  }
}
