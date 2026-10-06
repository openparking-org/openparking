import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../services/session_service.dart';
import '../../services/signalr_service.dart';
import '../auth_provider.dart';

final sessionServiceProvider = Provider<SessionService>((ref) {
  return SessionService();
});

class SessionStateNotifier
    extends StateNotifier<AsyncValue<ParkingSessionModel?>> {
  final SessionService _sessionService;
  Timer? _pollingTimer;
  StreamSubscription<HubEvent>? _hubSub;

  SessionStateNotifier(this._sessionService, {Stream<HubEvent>? hubEvents})
      : super(const AsyncValue.loading()) {
    // Immediately refresh on SignalR session/overstay events.
    if (hubEvents != null) {
      _hubSub = hubEvents.listen((event) {
        if (event.type == 'SessionUpdated' ||
            event.type == 'OverstayAlert' ||
            event.type == 'PenaltyIssued') {
          _fetchSession();
        }
      });
    }
  }

  void startPolling() {
    _fetchSession();
    _pollingTimer?.cancel();
    _pollingTimer = Timer.periodic(const Duration(seconds: 30), (_) {
      _fetchSession();
    });
  }

  void stopPolling() {
    _pollingTimer?.cancel();
    _pollingTimer = null;
  }

  Future<void> _fetchSession() async {
    try {
      final session = await _sessionService.getActiveSession();
      if (mounted) {
        state = AsyncValue.data(session);
      }
    } catch (e, stackTrace) {
      if (mounted) {
        state = AsyncValue.error(e, stackTrace);
      }
    }
  }

  Future<void> refresh() => _fetchSession();

  void updateSession(ParkingSessionModel? session) {
    if (mounted) {
      state = AsyncValue.data(session);
    }
  }

  @override
  void dispose() {
    stopPolling();
    _hubSub?.cancel();
    super.dispose();
  }
}

final activeSessionProvider = StateNotifierProvider<SessionStateNotifier,
    AsyncValue<ParkingSessionModel?>>((ref) {
  final service = ref.watch(sessionServiceProvider);
  final hubEvents = ref.watch(hubEventProvider).asData?.value != null
      ? ref.watch(signalRServiceProvider).events
      : null;

  final notifier = SessionStateNotifier(service, hubEvents: hubEvents);

  // Auto-start polling if user is authenticated
  final authState = ref.watch(authProvider);
  if (authState.isAuthenticated) {
    notifier.startPolling();
  }

  ref.onDispose(() {
    notifier.stopPolling();
  });

  return notifier;
});
