import 'slot.dart';
import '../services/session_service.dart';

class BookingModel {
  final String id, userId, slotId, status, currency;
  final DateTime startTime, endTime;
  final String? vehiclePlate, zoneId, zoneName;
  final double estimatedFee;
  final SlotModel? slot;
  final ParkingSessionModel? session;
  final bool isPaid;
  BookingModel(
      {required this.id,
      required this.userId,
      required this.slotId,
      required this.startTime,
      required this.endTime,
      required this.status,
      required this.estimatedFee,
      this.vehiclePlate,
      this.zoneId,
      this.zoneName,
      this.slot,
      this.session,
      this.currency = 'USD',
      this.isPaid = false});
  factory BookingModel.fromJson(Map<String, dynamic> json) => BookingModel(
        id: json['id'].toString(),
        userId: json['userId'].toString(),
        slotId: json['slotId'].toString(),
        startTime: DateTime.parse(json['startTime']),
        endTime: DateTime.parse(json['endTime']),
        vehiclePlate: json['vehiclePlate'] as String?,
        status: json['status'].toString(),
        estimatedFee: (json['estimatedFee'] as num?)?.toDouble() ?? 0,
        zoneId: (json['zoneId'] ?? json['slot']?['zoneId'])?.toString(),
        zoneName:
            (json['zoneName'] ?? json['slot']?['zone']?['name'])?.toString(),
        currency: json['currency']?.toString() ?? 'USD',
        isPaid: json['isPaid'] == true,
        slot: json['slot'] == null
            ? null
            : SlotModel.fromJson(json['slot'] as Map<String, dynamic>),
        session: json['session'] == null
            ? null
            : ParkingSessionModel.fromJson(
                json['session'] as Map<String, dynamic>),
      );
}
