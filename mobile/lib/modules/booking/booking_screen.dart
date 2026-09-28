import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';
import '../../services/session_service.dart';
import '../../core/providers/session_provider.dart';
import 'qr_scanner_screen.dart';

class BookingScreen extends ConsumerWidget {
  const BookingScreen({super.key});
  Future<void> _openScanner(BuildContext context, WidgetRef ref, ParkingSessionModel? activeSession, {required QrScannerMode mode}) async {
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
      ref.read(activeSessionProvider.notifier).updateSession(
        result.status == 'Completed' ? null : result
      );
        if (result.status == 'Completed') {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text('Checked out of ${result.zoneName}! Total: \$${result.totalFee.toStringAsFixed(2)}'),
              backgroundColor: const Color(0xFF10B981),
            ),
          );
        } else {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text('Checked into ${result.zoneName} (${result.slotNumber})!'),
              backgroundColor: const Color(0xFF10B981),
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
      appBar: AppBar(
        title: Text(
          activeSession != null ? 'Live Parking Session' : 'My Booking & Digital Pass',
          style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 18),
        ),
        backgroundColor: const Color(0xFF111827),
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh, size: 20),
            tooltip: 'Refresh Status',
            onPressed: () => ref.read(activeSessionProvider.notifier).startPolling(), // Manually trigger refresh
          ),
        ],
      ),
      backgroundColor: const Color(0xFF0B0F19),
      body: sessionAsyncValue.when(
        data: (session) => SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: session != null
              ? _buildActiveSessionView(context, ref, session)
              : _buildBookingPassView(context, ref, null),
        ),
        loading: () => const Center(
          child: CircularProgressIndicator(
            valueColor: AlwaysStoppedAnimation<Color>(Color(0xFF6366F1)),
          ),
        ),
        error: (error, _) => Center(
          child: Text('Failed to load session: $error', style: const TextStyle(color: Colors.red)),
        ),
      ),
    );
  }

  /// Live Active Session Tracker (design.md §19.4)
  Widget _buildActiveSessionView(BuildContext context, WidgetRef ref, ParkingSessionModel session) {
    final estimatedFee = _estimateCurrentFee(session);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Active Status Card
        Container(
          padding: const EdgeInsets.all(22),
          decoration: BoxDecoration(
            color: const Color(0xFF1E293B),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: const Color(0x4D10B981)),
            boxShadow: const [
              BoxShadow(color: Colors.black45, blurRadius: 16, offset: Offset(0, 8)),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: const Color(0x1F10B981),
                      borderRadius: BorderRadius.circular(20),
                      border: Border.all(color: const Color(0x4D10B981)),
                    ),
                    child: const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(Icons.circle, color: Color(0xFF10B981), size: 10),
                        SizedBox(width: 6),
                        Text(
                          'ACTIVE SESSION',
                          style: TextStyle(color: Color(0xFF10B981), fontSize: 11, fontWeight: FontWeight.bold),
                        ),
                      ],
                    ),
                  ),
                  Text(
                    'Bay ${session.slotNumber}',
                    style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 16),
                  ),
                ],
              ),
              const SizedBox(height: 18),
              Text(
                session.zoneName,
                style: const TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 4),
              const Text(
                'Vehicle parked inside designated bay',
                style: TextStyle(color: Colors.white60, fontSize: 13),
              ),
              const Divider(color: Colors.white12, height: 32),

              // Duration and estimate tiles
              Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Row(
                          children: [
                            Icon(Icons.timer_outlined, color: Colors.grey, size: 14),
                            SizedBox(width: 4),
                            Text('Duration', style: TextStyle(color: Colors.grey, fontSize: 12)),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          _formatDuration(session.checkInTime),
                          style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold),
                        ),
                      ],
                    ),
                  ),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Row(
                          children: [
                            Icon(Icons.attach_money, color: Color(0xFF10B981), size: 14),
                            SizedBox(width: 2),
                            Text('Est. Fee', style: TextStyle(color: Colors.grey, fontSize: 12)),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '\$${estimatedFee.toStringAsFixed(2)}',
                          style: const TextStyle(color: Color(0xFF10B981), fontSize: 18, fontWeight: FontWeight.bold),
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
                  color: Colors.black26,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Checked in at ${session.checkInTime.hour.toString().padLeft(2, '0')}:${session.checkInTime.minute.toString().padLeft(2, '0')}',
                      style: const TextStyle(color: Colors.white70, fontSize: 12),
                    ),
                    Text(
                      'Rate: \$${session.hourlyRate.toStringAsFixed(2)}/hr',
                      style: const TextStyle(color: Colors.white70, fontSize: 12),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),

        const SizedBox(height: 24),

        // Scan to Check Out Button (design.md §19.4)
        ElevatedButton.icon(
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFFEF4444),
            minimumSize: const Size.fromHeight(52),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
            elevation: 4,
          ),
          icon: const Icon(Icons.qr_code_scanner, color: Colors.white, size: 22),
          label: const Text(
            'Scan Exit Gate QR to Check Out',
            style: TextStyle(color: Colors.white, fontSize: 15, fontWeight: FontWeight.bold),
          ),
          onPressed: () => _openScanner(context, ref, session, mode: QrScannerMode.exitCheckOut),
        ),
      ],
    );
  }

  /// Booking Pass View with QR code and Scan to Enter Button
  Widget _buildBookingPassView(BuildContext context, WidgetRef ref, ParkingSessionModel? activeSession) {
    return Column(
      children: [
        // Digital Pass Card
        Container(
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(20),
            boxShadow: const [
              BoxShadow(color: Colors.black45, blurRadius: 16, offset: Offset(0, 8)),
            ],
          ),
          child: Column(
            children: [
              const Text(
                'OPENPARKING PASS',
                style: TextStyle(fontWeight: FontWeight.bold, letterSpacing: 1.2, color: Colors.black87),
              ),
              const SizedBox(height: 12),
              QrImageView(
                data: 'openparking://session/start?bookingId=bk-9912&slotId=slot-a102',
                version: QrVersions.auto,
                size: 190.0,
              ),
              const SizedBox(height: 12),
              const Text(
                'Scan at Entry Gate Terminal or scan gate QR below',
                textAlign: TextAlign.center,
                style: TextStyle(color: Colors.grey, fontSize: 12),
              ),
            ],
          ),
        ),

        const SizedBox(height: 22),

        // Zone & Bay Details Card
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: const Color(0xFF1E293B),
            borderRadius: BorderRadius.circular(12),
          ),
          child: const Row(
            mainAxisAlignment: MainAxisAlignment.spaceAround,
            children: [
              Column(
                children: [
                  Text('Zone', style: TextStyle(color: Colors.grey, fontSize: 12)),
                  SizedBox(height: 4),
                  Text('Zone A', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                ],
              ),
              Column(
                children: [
                  Text('Bay', style: TextStyle(color: Colors.grey, fontSize: 12)),
                  SizedBox(height: 4),
                  Text('A-102', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                ],
              ),
              Column(
                children: [
                  Text('Rate', style: TextStyle(color: Colors.grey, fontSize: 12)),
                  SizedBox(height: 4),
                  Text('\$5.00/hr', style: TextStyle(color: Color(0xFF10B981), fontWeight: FontWeight.bold)),
                ],
              ),
            ],
          ),
        ),

        const SizedBox(height: 22),

        // Scan to Enter Button (design.md §5.1)
        ElevatedButton.icon(
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF6366F1),
            minimumSize: const Size.fromHeight(50),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
            elevation: 4,
          ),
          icon: const Icon(Icons.qr_code_scanner, color: Colors.white, size: 20),
          label: const Text(
            'Scan Gate QR to Enter',
            style: TextStyle(color: Colors.white, fontSize: 15, fontWeight: FontWeight.bold),
          ),
          onPressed: () => _openScanner(context, ref, activeSession, mode: QrScannerMode.entryCheckIn),
        ),
      ],
    );
  }
}
