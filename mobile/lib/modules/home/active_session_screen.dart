import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/providers/session_provider.dart';
import '../../core/theme.dart';
import '../../core/widgets/app_widgets.dart';
import '../../services/session_service.dart';
import '../../services/signalr_service.dart';

/// Shows the live active parking session with a ticking timer, real-time fee,
/// SignalR overstay alerts, and a QR check-out shortcut.
class ActiveSessionScreen extends ConsumerStatefulWidget {
  const ActiveSessionScreen({super.key});

  @override
  ConsumerState<ActiveSessionScreen> createState() =>
      _ActiveSessionScreenState();
}

class _ActiveSessionScreenState extends ConsumerState<ActiveSessionScreen>
    with TickerProviderStateMixin {
  Timer? _ticker;
  Duration _elapsed = Duration.zero;
  bool _overstayAlerted = false;
  late AnimationController _pulseController;
  late Animation<double> _pulseAnimation;

  @override
  void initState() {
    super.initState();
    _pulseController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat(reverse: true);
    _pulseAnimation = Tween<double>(begin: 0.85, end: 1.0).animate(
      CurvedAnimation(parent: _pulseController, curve: Curves.easeInOut),
    );

    // Connect to SignalR hub for real-time events.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(signalRServiceProvider).connect();
    });
  }

  void _startTicker(DateTime checkIn) {
    _ticker?.cancel();
    _elapsed = DateTime.now().difference(checkIn);
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) {
        setState(() {
          _elapsed = DateTime.now().difference(checkIn);
        });
      }
    });
  }

  void _showOverstayBanner() {
    if (_overstayAlerted) return;
    _overstayAlerted = true;
    ScaffoldMessenger.of(context).showMaterialBanner(
      MaterialBanner(
        backgroundColor: AppTheme.accentCritical.withValues(alpha: 0.1),
        leading: const Icon(Icons.warning_amber_rounded,
            color: AppTheme.accentCritical, size: 28),
        content: const Text(
          'Overstay detected! Please proceed to the exit gate immediately to avoid additional penalties.',
          style: TextStyle(
              color: AppTheme.accentCritical, fontWeight: FontWeight.w600),
        ),
        actions: [
          TextButton(
            onPressed: () =>
                ScaffoldMessenger.of(context).hideCurrentMaterialBanner(),
            child: const Text('Dismiss',
                style: TextStyle(color: AppTheme.accentCritical)),
          )
        ],
      ),
    );
  }

  String _formatDuration(Duration d) {
    final h = d.inHours.toString().padLeft(2, '0');
    final m = (d.inMinutes % 60).toString().padLeft(2, '0');
    final s = (d.inSeconds % 60).toString().padLeft(2, '0');
    return '$h:$m:$s';
  }

  double _estimateFee(ParkingSessionModel session) {
    final hours = _elapsed.inSeconds / 3600;
    return hours * session.hourlyRate + session.penaltyFee;
  }

  @override
  void dispose() {
    _ticker?.cancel();
    _pulseController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final sessionAsync = ref.watch(activeSessionProvider);

    // Listen for hub events to show overstay alerts.
    ref.listen<AsyncValue<HubEvent>>(hubEventProvider, (_, next) {
      next.whenData((event) {
        if (event.type == 'OverstayAlert') _showOverstayBanner();
      });
    });

    return Scaffold(
      backgroundColor: AppTheme.surface,
      appBar: AppBar(
        backgroundColor: AppTheme.surfacePure,
        title: Text('Active Session',
            style: AppTheme.titleMd.copyWith(fontWeight: FontWeight.w700)),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh, color: AppTheme.primary),
            onPressed: () => ref.read(activeSessionProvider.notifier).refresh(),
            tooltip: 'Refresh',
          )
        ],
      ),
      body: sessionAsync.when(
        loading: () => const Center(
            child: CircularProgressIndicator(color: AppTheme.primary)),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              const Icon(Icons.wifi_off_rounded,
                  size: 64, color: AppTheme.textTertiary),
              const SizedBox(height: 16),
              const Text('Could not load session', style: AppTheme.titleMd),
              const SizedBox(height: 8),
              Text('$e',
                  style:
                      AppTheme.bodyMd.copyWith(color: AppTheme.textSecondary),
                  textAlign: TextAlign.center),
              const SizedBox(height: 24),
              FilledButton.icon(
                onPressed: () =>
                    ref.read(activeSessionProvider.notifier).refresh(),
                icon: const Icon(Icons.refresh),
                label: const Text('Retry'),
              ),
            ]),
          ),
        ),
        data: (session) {
          if (session == null) {
            return _NoActiveSession(
                onFindParking: () => Navigator.pop(context));
          }
          // Start the live ticker based on check-in time.
          WidgetsBinding.instance.addPostFrameCallback((_) {
            _startTicker(session.checkInTime);
            if (session.overstayMinutes > 0) _showOverstayBanner();
          });
          return _SessionContent(
            session: session,
            elapsed: _elapsed,
            estimatedFee: _estimateFee(session),
            elapsedDisplay: _formatDuration(_elapsed),
            pulseAnimation: _pulseAnimation,
          );
        },
      ),
    );
  }
}

// ── Main session content ───────────────────────────────────────────────────────

class _SessionContent extends StatelessWidget {
  final ParkingSessionModel session;
  final Duration elapsed;
  final double estimatedFee;
  final String elapsedDisplay;
  final Animation<double> pulseAnimation;

  const _SessionContent({
    required this.session,
    required this.elapsed,
    required this.estimatedFee,
    required this.elapsedDisplay,
    required this.pulseAnimation,
  });

  bool get isOverstay => session.overstayMinutes > 0;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: pagePadding(context),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: 8),
          _LiveTimerCard(
              elapsed: elapsedDisplay,
              isOverstay: isOverstay,
              pulseAnimation: pulseAnimation),
          const SizedBox(height: 16),
          _FeeCard(
              session: session, estimatedFee: estimatedFee, elapsed: elapsed),
          const SizedBox(height: 16),
          _SessionDetailsCard(session: session),
          const SizedBox(height: 16),
          if (isOverstay) _OverstayWarningCard(session: session),
          if (isOverstay) const SizedBox(height: 16),
          const _SignalRStatusCard(),
          const SizedBox(height: 32),
        ],
      ),
    );
  }
}

// ── Live Timer Card ────────────────────────────────────────────────────────────

class _LiveTimerCard extends StatelessWidget {
  final String elapsed;
  final bool isOverstay;
  final Animation<double> pulseAnimation;

  const _LiveTimerCard(
      {required this.elapsed,
      required this.isOverstay,
      required this.pulseAnimation});

  @override
  Widget build(BuildContext context) {
    final bg = isOverstay ? AppTheme.accentCritical : AppTheme.primary;
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 36, horizontal: 24),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: isOverstay
              ? [
                  AppTheme.accentCritical,
                  AppTheme.accentCritical.withValues(alpha: 0.7)
                ]
              : [AppTheme.primary, AppTheme.primary.withValues(alpha: 0.7)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(AppTheme.radiusXl),
        boxShadow: [
          BoxShadow(
            color: bg.withValues(alpha: 0.35),
            blurRadius: 24,
            offset: const Offset(0, 8),
          )
        ],
      ),
      child: Column(children: [
        Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          ScaleTransition(
            scale: pulseAnimation,
            child: Container(
              width: 10,
              height: 10,
              decoration: const BoxDecoration(
                  color: Colors.white, shape: BoxShape.circle),
            ),
          ),
          const SizedBox(width: 8),
          Text(
            isOverstay ? 'OVERSTAY' : 'LIVE',
            style: AppTheme.labelMd.copyWith(
                color: Colors.white.withValues(alpha: 0.85), letterSpacing: 2),
          ),
        ]),
        const SizedBox(height: 20),
        Text(
          elapsed,
          style: const TextStyle(
            fontFamily: 'monospace',
            fontSize: 52,
            fontWeight: FontWeight.w800,
            color: Colors.white,
            letterSpacing: 2,
          ),
        ),
        const SizedBox(height: 8),
        Text(
          isOverstay ? 'Overstay in progress — exit now' : 'Time parked',
          style: AppTheme.bodyMd
              .copyWith(color: Colors.white.withValues(alpha: 0.8)),
        ),
      ]),
    );
  }
}

// ── Fee Card ──────────────────────────────────────────────────────────────────

class _FeeCard extends StatelessWidget {
  final ParkingSessionModel session;
  final double estimatedFee;
  final Duration elapsed;

  const _FeeCard(
      {required this.session,
      required this.estimatedFee,
      required this.elapsed});

  @override
  Widget build(BuildContext context) {
    final hours = elapsed.inSeconds / 3600;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Row(children: [
            Icon(Icons.receipt_long_outlined,
                color: AppTheme.primary, size: 20),
            SizedBox(width: 8),
            Text('Live Fee Estimate', style: AppTheme.labelLg),
          ]),
          const Divider(height: 24),
          _FeeRow(
            label: 'Parking',
            value:
                '${session.currency} ${(hours * session.hourlyRate).toStringAsFixed(2)}',
          ),
          if (session.penaltyFee > 0)
            _FeeRow(
              label: 'Overstay Penalty',
              value:
                  '${session.currency} ${session.penaltyFee.toStringAsFixed(2)}',
              color: AppTheme.accentCritical,
            ),
          const Divider(height: 16),
          _FeeRow(
            label: 'Estimated Total',
            value: '${session.currency} ${estimatedFee.toStringAsFixed(2)}',
            bold: true,
          ),
          const SizedBox(height: 8),
          Text(
            'Rate: ${session.currency} ${session.hourlyRate.toStringAsFixed(2)}/hr',
            style: AppTheme.bodySm.copyWith(color: AppTheme.textTertiary),
          ),
        ]),
      ),
    );
  }
}

class _FeeRow extends StatelessWidget {
  final String label;
  final String value;
  final bool bold;
  final Color? color;

  const _FeeRow(
      {required this.label,
      required this.value,
      this.bold = false,
      this.color});

  @override
  Widget build(BuildContext context) {
    final style = bold
        ? AppTheme.titleMd
        : AppTheme.bodyMd.copyWith(color: AppTheme.textSecondary);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
        Text(label, style: style),
        Text(value,
            style: style.copyWith(color: color ?? (bold ? null : null))),
      ]),
    );
  }
}

// ── Session Details Card ───────────────────────────────────────────────────────

class _SessionDetailsCard extends StatelessWidget {
  final ParkingSessionModel session;
  const _SessionDetailsCard({required this.session});

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Row(children: [
            Icon(Icons.local_parking_outlined,
                color: AppTheme.primary, size: 20),
            SizedBox(width: 8),
            Text('Session Details', style: AppTheme.labelLg),
          ]),
          const Divider(height: 24),
          _DetailRow(
              icon: Icons.location_city_outlined,
              label: 'Zone',
              value: session.zoneName),
          _DetailRow(
              icon: Icons.grid_3x3_outlined,
              label: 'Slot',
              value: session.slotNumber),
          _DetailRow(
              icon: Icons.directions_car_outlined,
              label: 'Vehicle',
              value: session.vehiclePlate.isNotEmpty
                  ? session.vehiclePlate
                  : 'N/A'),
          _DetailRow(
              icon: Icons.login_outlined,
              label: 'Checked in',
              value: _formatTime(session.checkInTime)),
          _DetailRow(
              icon: Icons.circle_outlined,
              label: 'Status',
              value: session.status,
              valueColor: session.status == 'Active'
                  ? AppTheme.accentSuccess
                  : AppTheme.accentCritical),
        ]),
      ),
    );
  }

  String _formatTime(DateTime dt) {
    final local = dt.toLocal();
    final h = local.hour.toString().padLeft(2, '0');
    final m = local.minute.toString().padLeft(2, '0');
    return '${local.day}/${local.month} $h:$m';
  }
}

class _DetailRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;
  final Color? valueColor;

  const _DetailRow(
      {required this.icon,
      required this.label,
      required this.value,
      this.valueColor});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(children: [
        Icon(icon, size: 18, color: AppTheme.textTertiary),
        const SizedBox(width: 12),
        Expanded(
            child: Text(label,
                style:
                    AppTheme.bodyMd.copyWith(color: AppTheme.textSecondary))),
        Text(value,
            style: AppTheme.labelMd.copyWith(
                color: valueColor ?? AppTheme.textPrimary,
                fontWeight: FontWeight.w600)),
      ]),
    );
  }
}

// ── Overstay Warning Card ─────────────────────────────────────────────────────

class _OverstayWarningCard extends StatelessWidget {
  final ParkingSessionModel session;
  const _OverstayWarningCard({required this.session});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: AppTheme.accentCritical.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(AppTheme.radiusLg),
        border:
            Border.all(color: AppTheme.accentCritical.withValues(alpha: 0.3)),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          const Icon(Icons.warning_amber_rounded,
              color: AppTheme.accentCritical, size: 22),
          const SizedBox(width: 8),
          Text('Overstay Detected',
              style: AppTheme.labelLg.copyWith(color: AppTheme.accentCritical)),
        ]),
        const SizedBox(height: 12),
        Text(
          'You have exceeded your booked duration by ${session.overstayMinutes} minute${session.overstayMinutes == 1 ? '' : 's'}. '
          'Please proceed to the exit gate immediately. Additional penalties may be applied.',
          style: AppTheme.bodyMd.copyWith(color: AppTheme.accentCritical),
        ),
      ]),
    );
  }
}

// ── SignalR Status Card ───────────────────────────────────────────────────────

class _SignalRStatusCard extends ConsumerWidget {
  const _SignalRStatusCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final hubState = ref.watch(hubEventProvider);
    final isConnected = hubState is AsyncData || hubState is AsyncLoading;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: AppTheme.surfacePure,
        borderRadius: BorderRadius.circular(AppTheme.radiusMd),
        border: Border.all(color: AppTheme.surfaceMuted),
      ),
      child: Row(children: [
        Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(
            color: isConnected ? AppTheme.accentSuccess : AppTheme.textTertiary,
            shape: BoxShape.circle,
          ),
        ),
        const SizedBox(width: 10),
        Text(
          isConnected
              ? 'Real-time updates connected'
              : 'Connecting to real-time updates…',
          style: AppTheme.bodySm.copyWith(color: AppTheme.textSecondary),
        ),
      ]),
    );
  }
}

// ── No Active Session state ────────────────────────────────────────────────────

class _NoActiveSession extends StatelessWidget {
  final VoidCallback onFindParking;
  const _NoActiveSession({required this.onFindParking});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(40),
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Icon(Icons.local_parking_outlined,
              size: 72, color: AppTheme.textTertiary.withValues(alpha: 0.4)),
          const SizedBox(height: 24),
          Text('No Active Session',
              style: AppTheme.titleMd.copyWith(fontWeight: FontWeight.w700)),
          const SizedBox(height: 8),
          Text(
            'You don\'t have an active parking session right now.',
            textAlign: TextAlign.center,
            style: AppTheme.bodyMd.copyWith(color: AppTheme.textSecondary),
          ),
          const SizedBox(height: 32),
          FilledButton.icon(
            onPressed: onFindParking,
            icon: const Icon(Icons.search),
            label: const Text('Find Parking'),
          ),
        ]),
      ),
    );
  }
}
