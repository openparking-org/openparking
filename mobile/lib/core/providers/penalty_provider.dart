import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../models/penalty.dart';
import '../../services/penalty_service.dart';

class PenaltyListState {
  final List<PenaltyModel> penalties;
  final bool isLoading;
  final bool isDisputing;
  final String? error;

  PenaltyListState({
    this.penalties = const [],
    this.isLoading = false,
    this.isDisputing = false,
    this.error,
  });

  PenaltyListState copyWith({
    List<PenaltyModel>? penalties,
    bool? isLoading,
    bool? isDisputing,
    String? error,
  }) {
    return PenaltyListState(
      penalties: penalties ?? this.penalties,
      isLoading: isLoading ?? this.isLoading,
      isDisputing: isDisputing ?? this.isDisputing,
      error: error,
    );
  }
}

class PenaltyNotifier extends StateNotifier<PenaltyListState> {
  final PenaltyService _service;

  PenaltyNotifier(this._service) : super(PenaltyListState()) {
    fetchPenalties();
  }

  Future<void> fetchPenalties() async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final response = await _service.getMyPenalties();
      state = state.copyWith(penalties: response.items, isLoading: false);
    } catch (e) {
      state = state.copyWith(isLoading: false, error: e.toString());
    }
  }

  Future<bool> disputePenalty(String penaltyId, String reason) async {
    state = state.copyWith(isDisputing: true, error: null);
    try {
      final success = await _service.disputePenalty(penaltyId, reason);
      if (success) {
        state = state.copyWith(
          isDisputing: false,
          penalties: state.penalties
              .map((p) => p.id == penaltyId
                  ? PenaltyModel(
                      id: p.id,
                      userId: p.userId,
                      sessionId: p.sessionId,
                      amount: p.amount,
                      status: 'Disputed',
                      reason: p.reason,
                      evidenceImageUrl: p.evidenceImageUrl,
                      issuedAt: p.issuedAt,
                      disputeNotes: reason,
                    )
                  : p)
              .toList(),
        );
      } else {
        state = state.copyWith(isDisputing: false, error: 'Dispute failed');
      }
      return success;
    } catch (e) {
      state = state.copyWith(isDisputing: false, error: e.toString());
      return false;
    }
  }
}

final penaltyServiceProvider = Provider<PenaltyService>((ref) => PenaltyService());

final penaltyProvider = StateNotifierProvider<PenaltyNotifier, PenaltyListState>((ref) {
  final service = ref.watch(penaltyServiceProvider);
  return PenaltyNotifier(service);
});
