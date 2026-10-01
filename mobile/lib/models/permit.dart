class DisabilityPermitModel {
  final String id;
  final String userId;
  final String permitNumber;
  final String issuingAuthority;
  final DateTime expiryDate;
  final String? documentImageUrl;
  final String status; // Pending, Approved, Rejected
  final String? rejectionNotes;

  DisabilityPermitModel({
    required this.id,
    required this.userId,
    required this.permitNumber,
    required this.issuingAuthority,
    required this.expiryDate,
    this.documentImageUrl,
    required this.status,
    this.rejectionNotes,
  });

  factory DisabilityPermitModel.fromJson(Map<String, dynamic> json) {
    return DisabilityPermitModel(
      id: json['id']?.toString() ?? '',
      userId: json['userId']?.toString() ?? '',
      permitNumber: json['permitNumber']?.toString() ?? '',
      issuingAuthority: json['issuingAuthority']?.toString() ?? '',
      expiryDate: json['expiryDate'] != null
          ? DateTime.parse(json['expiryDate'].toString())
          : DateTime.now().add(const Duration(days: 365)),
      documentImageUrl: json['documentImageUrl']?.toString(),
      status: json['status']?.toString() ?? 'Pending',
      rejectionNotes: json['rejectionNotes']?.toString(),
    );
  }
}
