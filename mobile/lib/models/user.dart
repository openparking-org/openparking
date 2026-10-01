class UserModel {
  final String id;
  final String email;
  final String fullName;
  final String role; // Driver, ParkingAdmin, SystemAdmin
  final bool hasDisabilityPermit;

  UserModel({
    required this.id,
    required this.email,
    required this.fullName,
    required this.role,
    required this.hasDisabilityPermit,
  });

  factory UserModel.fromJson(Map<String, dynamic> json) {
    return UserModel(
      id: json['id']?.toString() ?? '',
      email: json['email']?.toString() ?? '',
      fullName: json['fullName']?.toString() ?? '',
      role: json['role']?.toString() ?? 'Driver',
      hasDisabilityPermit: json['hasDisabilityPermit'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'email': email,
        'fullName': fullName,
        'role': role,
        'hasDisabilityPermit': hasDisabilityPermit,
      };
}
