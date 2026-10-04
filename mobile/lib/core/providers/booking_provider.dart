import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../models/booking.dart';
import '../../services/booking_service.dart';
import '../auth_provider.dart';

class BookingListState {
  final List<BookingModel> bookings;
  final bool isLoading, isCreating, isLoadingMore, hasNextPage;
  final int page;
  final BookingModel? createdBooking;
  final String? error;
  BookingListState(
      {this.bookings = const [],
      this.isLoading = false,
      this.isCreating = false,
      this.isLoadingMore = false,
      this.hasNextPage = false,
      this.page = 1,
      this.createdBooking,
      this.error});
  BookingListState copyWith(
          {List<BookingModel>? bookings,
          bool? isLoading,
          bool? isCreating,
          bool? isLoadingMore,
          bool? hasNextPage,
          int? page,
          BookingModel? createdBooking,
          String? error}) =>
      BookingListState(
          bookings: bookings ?? this.bookings,
          isLoading: isLoading ?? this.isLoading,
          isCreating: isCreating ?? this.isCreating,
          isLoadingMore: isLoadingMore ?? this.isLoadingMore,
          hasNextPage: hasNextPage ?? this.hasNextPage,
          page: page ?? this.page,
          createdBooking: createdBooking,
          error: error);
}

class BookingNotifier extends StateNotifier<BookingListState> {
  final BookingService _service;
  Timer? _timer;
  bool _fetching = false;
  BookingNotifier(this._service, {bool authenticated = false})
      : super(BookingListState()) {
    if (authenticated) {
      fetchUserBookings();
      _timer = Timer.periodic(
          const Duration(seconds: 30), (_) => fetchUserBookings(silent: true));
    }
  }
  Future<void> fetchUserBookings({bool silent = false}) async {
    if (_fetching) return;
    _fetching = true;
    state = state.copyWith(isLoading: !silent);
    try {
      final collected = <BookingModel>[];
      final pages = silent ? state.page : 1;
      var hasNext = false;
      for (var page = 1; page <= pages; page++) {
        final result = await _service.getUserBookings(page: page);
        collected.addAll(result.items);
        hasNext = result.hasNextPage;
        if (!result.hasNextPage) {
          break;
        }
      }
      if (mounted) {
        state = state.copyWith(
            bookings: collected,
            isLoading: false,
            hasNextPage: hasNext,
            page: pages);
      }
    } catch (e) {
      if (mounted) state = state.copyWith(isLoading: false, error: '$e');
    } finally {
      _fetching = false;
    }
  }

  Future<void> loadMore() async {
    if (_fetching || !state.hasNextPage) return;
    _fetching = true;
    state = state.copyWith(isLoadingMore: true);
    try {
      final result = await _service.getUserBookings(page: state.page + 1);
      if (mounted) {
        state = state.copyWith(
            bookings: [...state.bookings, ...result.items],
            page: result.page,
            hasNextPage: result.hasNextPage,
            isLoadingMore: false);
      }
    } catch (e) {
      if (mounted) state = state.copyWith(isLoadingMore: false, error: '$e');
    } finally {
      _fetching = false;
    }
  }

  Future<BookingModel?> createBooking(
      {required String slotId,
      required DateTime startTime,
      required DateTime endTime,
      String? vehiclePlate}) async {
    if (state.isCreating) return null;
    state = state.copyWith(isCreating: true);
    try {
      final booking = await _service.createBooking(
          slotId: slotId,
          startTime: startTime,
          endTime: endTime,
          vehiclePlate: vehiclePlate);
      if (mounted) {
        state = state.copyWith(
            isCreating: false,
            createdBooking: booking,
            bookings: [booking, ...state.bookings]);
      }
      return booking;
    } catch (e) {
      if (mounted) state = state.copyWith(isCreating: false, error: '$e');
      return null;
    }
  }

  Future<bool> cancelBooking(String id) async {
    try {
      await _service.cancelBooking(id);
      await fetchUserBookings();
      return true;
    } catch (e) {
      if (mounted) state = state.copyWith(error: '$e');
      return false;
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }
}

final bookingServiceProvider =
    Provider<BookingService>((ref) => BookingService());
final bookingProvider =
    StateNotifierProvider<BookingNotifier, BookingListState>((ref) {
  final userId = ref.watch(authProvider.select((auth) => auth.user?.id));
  return BookingNotifier(ref.watch(bookingServiceProvider),
      authenticated: userId != null);
});
