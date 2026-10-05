import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../auth_provider.dart';
import '../../models/permit.dart';
import '../../services/permit_service.dart';

class PermitState {
  final DisabilityPermitModel? permit;
  final bool isLoading;
  final bool isSubmitting;
  final String? error;

  PermitState({
    this.permit,
    this.isLoading = false,
    this.isSubmitting = false,
    this.error,
  });

  PermitState copyWith({
    DisabilityPermitModel? permit,
    bool? isLoading,
    bool? isSubmitting,
    String? error,
  }) {
    return PermitState(
      permit: permit ?? this.permit,
      isLoading: isLoading ?? this.isLoading,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      error: error,
    );
  }
}

class PermitNotifier extends StateNotifier<PermitState> {
  final PermitService _service;

  PermitNotifier(this._service) : super(PermitState()) {
    fetchPermitStatus();
  }

  Future<void> fetchPermitStatus() async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final permit = await _service.getMyPermit();
      if (mounted) state = PermitState(permit: permit);
    } catch (e) {
      if (mounted) {
        state = state.copyWith(isLoading: false, error: e.toString());
      }
    }
  }

  Future<bool> submitPermit({
    required String userId,
    required String permitNumber,
    required String issuingAuthority,
    required DateTime expiryDate,
    String? documentBase64,
    String? documentImageUrl,
  }) async {
    state = state.copyWith(isSubmitting: true, error: null);
    try {
      final permit = await _service.submitPermit(
        userId: userId,
        permitNumber: permitNumber,
        issuingAuthority: issuingAuthority,
        expiryDate: expiryDate,
        documentBase64: documentBase64,
        documentImageUrl: documentImageUrl,
      );
      state = state.copyWith(permit: permit, isSubmitting: false);
      return true;
    } catch (e) {
      state = state.copyWith(isSubmitting: false, error: e.toString());
      return false;
    }
  }
}

final permitServiceProvider = Provider<PermitService>((ref) => PermitService());

final permitProvider =
    StateNotifierProvider<PermitNotifier, PermitState>((ref) {
  ref.watch(authProvider.select((auth) => auth.user?.id));
  final service = ref.watch(permitServiceProvider);
  return PermitNotifier(service);
});
