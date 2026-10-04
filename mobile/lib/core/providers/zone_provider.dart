import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../models/zone.dart';
import '../../services/zone_service.dart';

class ZoneListState {
  final List<ZoneModel> zones;
  final bool isLoading;
  final bool isLoadingMore;
  final String searchQuery;
  final String filter;
  final int page;
  final int pageSize;
  final bool hasNextPage;
  final String? error;
  final ZoneModel? selectedZone;

  ZoneListState({
    this.zones = const [],
    this.isLoading = false,
    this.isLoadingMore = false,
    this.searchQuery = '',
    this.filter = 'All',
    this.page = 1,
    this.pageSize = 10,
    this.hasNextPage = false,
    this.error,
    this.selectedZone,
  });

  ZoneListState copyWith({
    List<ZoneModel>? zones,
    bool? isLoading,
    bool? isLoadingMore,
    String? searchQuery,
    String? filter,
    int? page,
    int? pageSize,
    bool? hasNextPage,
    String? error,
    ZoneModel? selectedZone,
  }) {
    return ZoneListState(
      zones: zones ?? this.zones,
      isLoading: isLoading ?? this.isLoading,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      searchQuery: searchQuery ?? this.searchQuery,
      filter: filter ?? this.filter,
      page: page ?? this.page,
      pageSize: pageSize ?? this.pageSize,
      hasNextPage: hasNextPage ?? this.hasNextPage,
      error: error,
      selectedZone: selectedZone ?? this.selectedZone,
    );
  }
}

class ZoneListNotifier extends StateNotifier<ZoneListState> {
  final ZoneService _service;
  int _requestId = 0;

  ZoneListNotifier(this._service) : super(ZoneListState()) {
    fetchZones();
  }

  Future<void> fetchZones(
      {String? query, String? filter, bool reset = true}) async {
    final requestId = reset ? ++_requestId : _requestId;
    if (reset) {
      state = state.copyWith(
        isLoading: true,
        page: 1,
        searchQuery: query ?? state.searchQuery,
        filter: filter ?? state.filter,
        isLoadingMore: false,
        error: null,
      );
    } else {
      if (!state.hasNextPage || state.isLoadingMore || state.isLoading) return;
      state = state.copyWith(isLoadingMore: true, error: null);
    }

    try {
      final response = await _service.getZones(
        page: reset ? 1 : state.page + 1,
        pageSize: state.pageSize,
        search: query ?? state.searchQuery,
        filter: filter ?? state.filter,
      );

      if (!mounted || requestId != _requestId) return;
      final newZones =
          reset ? response.items : [...state.zones, ...response.items];

      state = state.copyWith(
        zones: newZones,
        isLoading: false,
        isLoadingMore: false,
        page: response.page,
        hasNextPage: response.hasNextPage,
      );
    } catch (e) {
      if (!mounted || requestId != _requestId) return;
      state = state.copyWith(
        isLoading: false,
        isLoadingMore: false,
        error: e.toString(),
      );
    }
  }

  Future<void> loadZoneDetail(String zoneId) async {
    state = ZoneListState(
        zones: state.zones,
        selectedZone: null,
        isLoading: true,
        searchQuery: state.searchQuery,
        filter: state.filter,
        page: state.page,
        hasNextPage: state.hasNextPage);
    try {
      final detail = await _service.getZoneDetail(zoneId);
      if (detail != null) {
        if (mounted) {
          state = state.copyWith(selectedZone: detail, isLoading: false);
        }
      }
    } catch (e) {
      if (mounted)
        state = state.copyWith(isLoading: false, error: e.toString());
    }
  }

  void selectZone(ZoneModel zone) {
    state = state.copyWith(selectedZone: zone);
  }
}

final zoneServiceProvider = Provider<ZoneService>((ref) => ZoneService());

final zoneListProvider =
    StateNotifierProvider<ZoneListNotifier, ZoneListState>((ref) {
  final service = ref.watch(zoneServiceProvider);
  return ZoneListNotifier(service);
});
