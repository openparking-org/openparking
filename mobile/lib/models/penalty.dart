class PenaltyModel {
  final String id;
  final String userId;
  final String? sessionId;
  final double amount;
  final String status; // Approved, Disputed, Waived, Paid, Pending, Rejected
  final String reason;
  final String? evidenceImageUrl;
  final DateTime issuedAt;
  final String? disputeNotes;

  PenaltyModel({
    required this.id,
    required this.userId,
    this.sessionId,
    required this.amount,
    required this.status,
    required this.reason,
    this.evidenceImageUrl,
    required this.issuedAt,
    this.disputeNotes,
  });

  bool get isDisputable {
    if (status.toLowerCase() != 'approved') return false;
    final daysSinceIssued = DateTime.now().difference(issuedAt).inDays;
    return daysSinceIssued <= 7;
  }

  factory PenaltyModel.fromJson(Map<String, dynamic> json) {
    return PenaltyModel(
      id: json['id']?.toString() ?? '',
      userId: json['userId']?.toString() ?? '',
      sessionId: json['sessionId']?.toString(),
      amount: (json['amount'] as num?)?.toDouble() ?? 0.0,
      status: json['status']?.toString() ?? 'Approved',
      reason: json['reason']?.toString() ?? 'Overstay',
      evidenceImageUrl: json['evidenceImageUrl']?.toString(),
      issuedAt: json['issuedAt'] != null
          ? DateTime.parse(json['issuedAt'].toString())
          : DateTime.now(),
      disputeNotes: json['disputeNotes']?.toString(),
    );
  }
}
