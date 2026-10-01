import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../models/floor_plan.dart';
import '../../models/route.dart';
import '../../services/floor_plan_service.dart';

class FloorPlanState {
  final List<FloorPlanModel> floorPlans;
  final FloorPlanModel? selectedFloorPlan;
  final NavigationRouteModel? navigationRoute;
  final bool isLoading;
  final String? error;

  FloorPlanState({
    this.floorPlans = const [],
    this.selectedFloorPlan,
    this.navigationRoute,
    this.isLoading = false,
    this.error,
  });

  FloorPlanState copyWith({
    List<FloorPlanModel>? floorPlans,
    FloorPlanModel? selectedFloorPlan,
    NavigationRouteModel? navigationRoute,
    bool? isLoading,
    String? error,
  }) {
    return FloorPlanState(
      floorPlans: floorPlans ?? this.floorPlans,
      selectedFloorPlan: selectedFloorPlan ?? this.selectedFloorPlan,
      navigationRoute: navigationRoute ?? this.navigationRoute,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

class FloorPlanNotifier extends StateNotifier<FloorPlanState> {
  final FloorPlanService _service;

  FloorPlanNotifier(this._service) : super(FloorPlanState());

  Future<void> loadFloorPlansForZone(String zoneId) async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final plans = await _service.getFloorPlansForZone(zoneId);
      state = state.copyWith(
        floorPlans: plans,
        selectedFloorPlan: plans.isNotEmpty ? plans.first : null,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, error: e.toString());
    }
  }

  void selectFloorPlan(FloorPlanModel plan) {
    state = state.copyWith(selectedFloorPlan: plan);
  }

  Future<void> loadNavigationRoute({
    required String zoneId,
    required String targetSlotId,
    String? bookingId,
  }) async {
    try {
      final route = await _service.getNavigationRoute(
        zoneId: zoneId,
        targetSlotId: targetSlotId,
        bookingId: bookingId,
      );
      state = state.copyWith(navigationRoute: route);
    } catch (e) {
      state = state.copyWith(error: e.toString());
    }
  }
}

final floorPlanServiceProvider = Provider<FloorPlanService>((ref) => FloorPlanService());

final floorPlanProvider = StateNotifierProvider<FloorPlanNotifier, FloorPlanState>((ref) {
  final service = ref.watch(floorPlanServiceProvider);
  return FloorPlanNotifier(service);
});
