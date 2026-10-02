import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';
import '../../services/session_service.dart';
import '../../core/providers/session_provider.dart';
import '../../core/theme.dart';
import 'qr_scanner_screen.dart';

class BookingScreen extends ConsumerWidget {
  const BookingScreen({super.key});
  Future<void> _openScanner(
      BuildContext context, WidgetRef ref, ParkingSessionModel? activeSession,
      {required QrScannerMode mode}) async {
    final result = await Navigator.of(context).push<ParkingSessionModel>(
      MaterialPageRoute(
        builder: (context) => QrScannerScreen(
          mode: mode,
          existingSessionId: activeSession?.id,
          existingBookingId: activeSession?.bookingId,
        ),
      ),
    );

    if (result != null && context.mounted) {
      ref
          .read(activeSessionProvider.notifier)
          .updateSession(result.status == 'Completed' ? null : result);
      if (result.status == 'Completed') {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
                'Checked out of ${result.zoneName}! Total: \$${result.totalFee.toStringAsFixed(2)}'),
            backgroundColor: AppTheme.accentSuccess,
          ),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content:
                Text('Checked into ${result.zoneName} (${result.slotNumber})!'),
            backgroundColor: AppTheme.accentSuccess,
          ),
        );
      }
    }
  }

  String _formatDuration(DateTime checkInTime) {
    final diff = DateTime.now().difference(checkInTime);
    final hours = diff.inHours;
    final minutes = diff.inMinutes % 60;
    return '${hours}h ${minutes}m';
  }

  double _estimateCurrentFee(ParkingSessionModel session) {
    final diff = DateTime.now().difference(session.checkInTime);
    final minutes = diff.inMinutes;
    final billableBlocks = (minutes / 15.0).ceil();
    final billableHours = (billableBlocks * 15.0) / 60.0;
    return (billableHours * session.hourlyRate);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sessionAsyncValue = ref.watch(activeSessionProvider);
    final activeSession = sessionAsyncValue.valueOrNull;

    return Scaffold(
      backgroundColor: AppTheme.surface,
      body: CustomScrollView(
        slivers: [
          SliverAppBar(
            floating: true,
            snap: true,
            backgroundColor: AppTheme.surfacePure,
            surfaceTintColor: Colors.transparent,
            title: Text(
              activeSession != null
                  ? 'Live Parking Session'
                  : 'My Booking & Digital Pass',
              style: AppTheme.titleMd.copyWith(fontWeight: FontWeight.w700),
            ),
            actions: [
              IconButton(
                icon: const Icon(Icons.refresh, size: 20),
                color: AppTheme.primary,
                tooltip: 'Refresh Status',
                onPressed: () => ref
                    .read(activeSessionProvider.notifier)
                    .startPolling(),
              ),
            ],
          ),
          SliverPadding(
            padding: const EdgeInsets.all(AppTheme.margin),
            sliver: SliverList(
              delegate: SliverChildListDelegate([
                sessionAsyncValue.when(
                  data: (session) => session != null
                      ? _buildActiveSessionView(context, ref, session)
                      : _buildBookingPassView(context, ref, null),
                  loading: () => const Padding(
                    padding: EdgeInsets.all(48),
                    child: Center(
                      child: CircularProgressIndicator(
                        color: AppTheme.primary,
                        strokeWidth: 2,
                      ),
                    ),
                  ),
                  error: (error, _) => Center(
                    child: Text(
                      'Failed to load session: $error',
                      style: AppTheme.bodyMd.copyWith(
                        color: AppTheme.accentCritical,
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: 80),
              ]),
            ),
          ),
        ],
      ),
    );
  }

  /// Live Active Session Tracker
  Widget _buildActiveSessionView(
      BuildContext context, WidgetRef ref, ParkingSessionModel session) {
    final estimatedFee = _estimateCurrentFee(session);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Active Status Card
        Container(
          padding: const EdgeInsets.all(AppTheme.spaceMd + 4),
          decoration: BoxDecoration(
            color: AppTheme.surfacePure,
            borderRadius: BorderRadius.circular(AppTheme.radiusXl),
            border: Border.all(
              color: AppTheme.accentSuccess.withValues(alpha: 0.3),
            ),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.04),
                blurRadius: 16,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppTheme.accentSuccess.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(100),
                      border: Border.all(
                        color: AppTheme.accentSuccess.withValues(alpha: 0.3),
                      ),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Container(
                          width: 6,
                          height: 6,
                          decoration: const BoxDecoration(
                            color: AppTheme.accentSuccess,
                            shape: BoxShape.circle,
                          ),
                        ),
                        const SizedBox(width: 6),
                        Text(
                          'ACTIVE SESSION',
                          style: AppTheme.labelSm.copyWith(
                            color: AppTheme.accentSuccess,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Text(
                    'Bay ${session.slotNumber}',
                    style: AppTheme.labelLg.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              Text(
                session.zoneName,
                style: AppTheme.headlineSm,
              ),
              const SizedBox(height: 4),
              Text(
                'Vehicle parked inside designated bay',
                style: AppTheme.bodySm.copyWith(
                  color: AppTheme.textSecondary,
                ),
              ),
              const Divider(height: 32, color: AppTheme.borderSubtle),

              // Duration and estimate tiles
              Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            const Icon(Icons.timer_outlined,
                                color: AppTheme.textTertiary, size: 14),
                            const SizedBox(width: 4),
                            Text(
                              'Duration',
                              style: AppTheme.labelSm.copyWith(
                                color: AppTheme.textTertiary,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          _formatDuration(session.checkInTime),
                          style: AppTheme.titleMd.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            const Icon(Icons.attach_money,
                                color: AppTheme.accentSuccess, size: 14),
                            const SizedBox(width: 2),
                            Text(
                              'Est. Fee',
                              style: AppTheme.labelSm.copyWith(
                                color: AppTheme.textTertiary,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '\$${estimatedFee.toStringAsFixed(2)}',
                          style: AppTheme.titleMd.copyWith(
                            color: AppTheme.accentSuccess,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),

              // Check-in and rate info
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: AppTheme.surfaceSubtle,
                  borderRadius: BorderRadius.circular(AppTheme.radiusLg),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Checked in at ${session.checkInTime.hour.toString().padLeft(2, '0')}:${session.checkInTime.minute.toString().padLeft(2, '0')}',
                      style: AppTheme.bodySm.copyWith(
                        color: AppTheme.textSecondary,
                      ),
                    ),
                    Text(
                      'Rate: \$${session.hourlyRate.toStringAsFixed(2)}/hr',
                      style: AppTheme.bodySm.copyWith(
                        color: AppTheme.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),

        const SizedBox(height: AppTheme.spaceLg),

        // Scan to Check Out Button
        SizedBox(
          height: 52,
          child: ElevatedButton.icon(
            style: ElevatedButton.styleFrom(
              backgroundColor: AppTheme.accentCritical,
              foregroundColor: AppTheme.onPrimary,
              elevation: 0,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppTheme.radiusXl),
              ),
            ),
            icon: const Icon(Icons.qr_code_scanner, size: 22),
            label: Text(
              'Scan Exit Gate QR to Check Out',
              style: AppTheme.labelLg.copyWith(
                color: AppTheme.onPrimary,
              ),
            ),
            onPressed: () => _openScanner(context, ref, session,
                mode: QrScannerMode.exitCheckOut),
          ),
        ),
      ],
    );
  }

  /// Booking Pass View with QR code and Scan to Enter Button
  Widget _buildBookingPassView(
      BuildContext context, WidgetRef ref, ParkingSessionModel? activeSession) {
    return Column(
      children: [
        // Digital Pass Card
        Container(
          padding: const EdgeInsets.all(AppTheme.spaceLg),
          decoration: BoxDecoration(
            color: AppTheme.surfacePure,
            borderRadius: BorderRadius.circular(AppTheme.radiusXl),
            border: Border.all(color: AppTheme.borderSubtle),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.04),
                blurRadius: 16,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Column(
            children: [
              // Header
              Container(
                padding: const EdgeInsets.symmetric(
                    horizontal: 12, vertical: 6),
                decoration: BoxDecoration(
                  color: AppTheme.surfaceSubtle,
                  borderRadius: BorderRadius.circular(100),
                  border: Border.all(color: AppTheme.borderSubtle),
                ),
                child: Text(
                  'OPENPARKING PASS',
                  style: AppTheme.labelSm.copyWith(
                    fontWeight: FontWeight.w700,
                    letterSpacing: 1.2,
                    color: AppTheme.textPrimary,
                  ),
                ),
              ),
              const SizedBox(height: 16),
              QrImageView(
                data:
                    'openparking://session/start?bookingId=bk-9912&slotId=slot-a102',
                version: QrVersions.auto,
                size: 190.0,
                dataModuleStyle: const QrDataModuleStyle(
                  color: AppTheme.primary,
                ),
                eyeStyle: const QrEyeStyle(
                  color: AppTheme.primary,
                ),
              ),
              const SizedBox(height: 12),
              Text(
                'Scan at Entry Gate Terminal or scan gate QR below',
                textAlign: TextAlign.center,
                style: AppTheme.bodySm.copyWith(
                  color: AppTheme.textTertiary,
                ),
              ),
            ],
          ),
        ),

        const SizedBox(height: AppTheme.spaceMd),

        // Zone & Bay Details Card
        Container(
          padding: const EdgeInsets.all(AppTheme.spaceMd),
          decoration: BoxDecoration(
            color: AppTheme.surfacePure,
            borderRadius: BorderRadius.circular(AppTheme.radiusXl),
            border: Border.all(color: AppTheme.borderSubtle),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceAround,
            children: [
              _InfoColumn(label: 'Zone', value: 'Zone A'),
              Container(
                width: 1,
                height: 32,
                color: AppTheme.borderSubtle,
              ),
              _InfoColumn(label: 'Bay', value: 'A-102'),
              Container(
                width: 1,
                height: 32,
                color: AppTheme.borderSubtle,
              ),
              _InfoColumn(
                label: 'Rate',
                value: '\$5.00/hr',
                valueColor: AppTheme.accentSuccess,
              ),
            ],
          ),
        ),

        const SizedBox(height: AppTheme.spaceMd),

        // Scan to Enter Button
        SizedBox(
          height: 50,
          child: ElevatedButton.icon(
            style: ElevatedButton.styleFrom(
              backgroundColor: AppTheme.primary,
              foregroundColor: AppTheme.onPrimary,
              elevation: 0,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppTheme.radiusXl),
              ),
            ),
            icon: const Icon(Icons.qr_code_scanner, size: 20),
            label: Text(
              'Scan Gate QR to Enter',
              style: AppTheme.labelLg.copyWith(
                color: AppTheme.onPrimary,
              ),
            ),
            onPressed: () => _openScanner(context, ref, activeSession,
                mode: QrScannerMode.entryCheckIn),
          ),
        ),
      ],
    );
  }
}

class _InfoColumn extends StatelessWidget {
  final String label;
  final String value;
  final Color? valueColor;

  const _InfoColumn({
    required this.label,
    required this.value,
    this.valueColor,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          label,
          style: AppTheme.labelSm.copyWith(
            color: AppTheme.textTertiary,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          value,
          style: AppTheme.labelLg.copyWith(
            color: valueColor ?? AppTheme.textPrimary,
            fontWeight: FontWeight.w700,
          ),
        ),
      ],
    );
  }
}
