import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../models/booking.dart';
import '../../services/booking_service.dart';

class BookingListState {
  final List<BookingModel> bookings;
  final bool isLoading;
  final bool isCreating;
  final BookingModel? createdBooking;
  final String? error;

  BookingListState({
    this.bookings = const [],
    this.isLoading = false,
    this.isCreating = false,
    this.createdBooking,
    this.error,
  });

  BookingListState copyWith({
    List<BookingModel>? bookings,
    bool? isLoading,
    bool? isCreating,
    BookingModel? createdBooking,
    String? error,
  }) {
    return BookingListState(
      bookings: bookings ?? this.bookings,
      isLoading: isLoading ?? this.isLoading,
      isCreating: isCreating ?? this.isCreating,
      createdBooking: createdBooking ?? this.createdBooking,
      error: error,
    );
  }
}

class BookingNotifier extends StateNotifier<BookingListState> {
  final BookingService _service;

  BookingNotifier(this._service) : super(BookingListState());

  Future<void> fetchUserBookings() async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final response = await _service.getUserBookings();
      state = state.copyWith(bookings: response.items, isLoading: false);
    } catch (e) {
      state = state.copyWith(isLoading: false, error: e.toString());
    }
  }

  Future<BookingModel?> createBooking({
    required String slotId,
    required DateTime startTime,
    required DateTime endTime,
    String? vehiclePlate,
  }) async {
    state = state.copyWith(isCreating: true, error: null, createdBooking: null);
    try {
      final booking = await _service.createBooking(
        slotId: slotId,
        startTime: startTime,
        endTime: endTime,
        vehiclePlate: vehiclePlate,
      );
      state = state.copyWith(
        isCreating: false,
        createdBooking: booking,
        bookings: [booking, ...state.bookings],
      );
      return booking;
    } catch (e) {
      state = state.copyWith(isCreating: false, error: e.toString());
      return null;
    }
  }

  Future<bool> cancelBooking(String bookingId) async {
    try {
      final success = await _service.cancelBooking(bookingId);
      if (success) {
        state = state.copyWith(
          bookings: state.bookings
              .map((b) => b.id == bookingId
                  ? BookingModel(
                      id: b.id,
                      userId: b.userId,
                      slotId: b.slotId,
                      startTime: b.startTime,
                      endTime: b.endTime,
                      vehiclePlate: b.vehiclePlate,
                      status: 'Cancelled',
                      qrCodeContent: b.qrCodeContent,
                      estimatedFee: b.estimatedFee,
                      slot: b.slot,
                    )
                  : b)
              .toList(),
        );
      }
      return success;
    } catch (e) {
      state = state.copyWith(error: e.toString());
      return false;
    }
  }
}

final bookingServiceProvider = Provider<BookingService>((ref) => BookingService());

final bookingProvider = StateNotifierProvider<BookingNotifier, BookingListState>((ref) {
  final service = ref.watch(bookingServiceProvider);
  return BookingNotifier(service);
});
