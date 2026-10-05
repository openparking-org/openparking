import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:signalr_netcore/signalr_client.dart';
import 'api_config.dart';

/// Events broadcast by the SignalR hub.
class HubEvent {
  final String type;
  final Map<String, dynamic> payload;
  const HubEvent(this.type, this.payload);
}

/// Wraps the SignalR [HubConnection], reconnects automatically, and exposes a
/// [Stream<HubEvent>] that UI layers can listen to.
class SignalRService {
  HubConnection? _connection;
  final _controller = StreamController<HubEvent>.broadcast();
  Timer? _reconnectTimer;
  bool _disposed = false;

  Stream<HubEvent> get events => _controller.stream;

  /// Derive the SignalR hub URL from the API base URL.
  String get _hubUrl {
    final base = ApiConfig.baseUrl.replaceAll(RegExp(r'/$'), '');
    return '$base/hubs/parking';
  }

  Future<void> connect() async {
    if (_disposed) return;
    try {
      _connection?.stop();
      final token = ApiConfig.authToken;
      _connection = HubConnectionBuilder()
          .withUrl(
            _hubUrl,
            options: HttpConnectionOptions(
              accessTokenFactory: token != null ? () async => token : null,
            ),
          )
          .withAutomaticReconnect(retryDelays: [2000, 5000, 10000, 30000])
          .build();

      // ── Hub event listeners ──────────────────────────────────────────────
      _connection!.on('SlotStateChanged', (args) {
        _emit('SlotStateChanged', args);
      });
      _connection!.on('SessionUpdated', (args) {
        _emit('SessionUpdated', args);
      });
      _connection!.on('OverstayAlert', (args) {
        _emit('OverstayAlert', args);
      });
      _connection!.on('PenaltyIssued', (args) {
        _emit('PenaltyIssued', args);
      });

      _connection!.onclose(({error}) => _scheduleReconnect());
      _connection!.onreconnecting(({error}) => {});
      _connection!.onreconnected(({connectionId}) => {});

      await _connection!.start();
    } catch (_) {
      _scheduleReconnect();
    }
  }

  void _emit(String type, List<Object?>? args) {
    if (_disposed) return;
    final payload = <String, dynamic>{};
    if (args != null && args.isNotEmpty && args.first is Map) {
      payload.addAll(Map<String, dynamic>.from(args.first as Map));
    }
    _controller.add(HubEvent(type, payload));
  }

  void _scheduleReconnect() {
    if (_disposed) return;
    _reconnectTimer?.cancel();
    _reconnectTimer = Timer(const Duration(seconds: 10), connect);
  }

  Future<void> disconnect() async {
    _reconnectTimer?.cancel();
    await _connection?.stop();
    _connection = null;
  }

  void dispose() {
    _disposed = true;
    _reconnectTimer?.cancel();
    _connection?.stop();
    _controller.close();
  }
}

// ── Riverpod providers ────────────────────────────────────────────────────────

final signalRServiceProvider = Provider<SignalRService>((ref) {
  final service = SignalRService();
  ref.onDispose(service.dispose);
  return service;
});

/// Fires whenever any hub event arrives so widgets can react immediately.
final hubEventProvider = StreamProvider<HubEvent>((ref) {
  final svc = ref.watch(signalRServiceProvider);
  return svc.events;
});
