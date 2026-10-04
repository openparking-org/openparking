import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../core/providers/booking_provider.dart';
import '../../core/providers/session_provider.dart';
import '../../models/booking.dart';
import '../space_availability/indoor_map_screen.dart';
import '../space_availability/zone_discovery_screen.dart';

class BookingScreen extends ConsumerWidget {
  const BookingScreen({super.key});
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(bookingProvider);
    final active = ref.watch(activeSessionProvider);
    Future<void> refresh() async => Future.wait([
          ref.read(bookingProvider.notifier).fetchUserBookings(),
          ref.read(activeSessionProvider.notifier).refresh(),
        ]);
    return Scaffold(
      appBar: AppBar(title: const Text('My Bookings'), actions: [
        IconButton(
            tooltip: 'Refresh bookings',
            icon: const Icon(Icons.refresh),
            onPressed: refresh),
      ]),
      body: RefreshIndicator(
          onRefresh: refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(16),
            children: [
              const Text(
                  'Give your vehicle number to the gate attendant. They will check you in and out.'),
              const SizedBox(height: 16),
              active.when(
                loading: () => const LinearProgressIndicator(),
                error: (e, _) =>
                    Text('Unable to load your current session: $e'),
                data: (session) => session == null
                    ? const SizedBox.shrink()
                    : Card(
                        child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                  session.status == 'OverstayDetected'
                                      ? 'Overstay detected'
                                      : 'Parking session active',
                                  style:
                                      Theme.of(context).textTheme.titleMedium),
                              Text(
                                  '${session.vehiclePlate} · ${session.zoneName} · ${session.slotNumber}'),
                              Text(
                                  'Entered ${DateFormat('MMM d, HH:mm').format(session.checkInTime.toLocal())}'),
                              Text(
                                  'Rate: ${session.currency} ${session.hourlyRate.toStringAsFixed(2)}/hour'),
                              const Text(
                                  'The final fee is calculated when the attendant checks you out.'),
                            ]),
                      )),
              ),
              if (state.isLoading) const LinearProgressIndicator(),
              if (state.error != null)
                Padding(
                    padding: const EdgeInsets.symmetric(vertical: 12),
                    child: Text(state.error!)),
              if (!state.isLoading &&
                  state.error == null &&
                  state.bookings.isEmpty) ...[
                const Padding(
                    padding: EdgeInsets.all(24),
                    child: Text('You have no reservations yet.')),
                FilledButton(
                    onPressed: () => Navigator.push(
                        context,
                        MaterialPageRoute<void>(
                            builder: (_) => const ZoneDiscoveryScreen())),
                    child: const Text('Find parking')),
              ],
              for (final booking in state.bookings)
                _BookingCard(booking: booking),
              if (state.hasNextPage)
                TextButton(
                    onPressed: state.isLoadingMore
                        ? null
                        : () => ref.read(bookingProvider.notifier).loadMore(),
                    child: Text(state.isLoadingMore
                        ? 'Loading…'
                        : 'Load more bookings')),
            ],
          )),
    );
  }
}

class _BookingCard extends ConsumerWidget {
  final BookingModel booking;
  const _BookingCard({required this.booking});
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = booking.session;
    final completed = session?.checkOutTime != null;
    return Card(
        child: Padding(
            padding: const EdgeInsets.all(16),
            child:
                Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(booking.zoneName ?? 'Parking reservation',
                  style: Theme.of(context).textTheme.titleMedium),
              Text(
                  '${booking.vehiclePlate ?? 'Vehicle number missing'} · Space ${booking.slot?.slotNumber ?? booking.slotId}'),
              Text(
                  '${DateFormat('MMM d, HH:mm').format(booking.startTime.toLocal())} – ${DateFormat('MMM d, HH:mm').format(booking.endTime.toLocal())}'),
              Text('Status: ${booking.status}'),
              Text(
                  '${completed ? 'Final charge' : 'Estimate'}: ${booking.currency} ${(completed ? session!.totalFee : booking.estimatedFee).toStringAsFixed(2)}'),
              if (completed)
                Text(booking.isPaid
                    ? 'Payment received'
                    : 'Payment due at the gate'),
              Wrap(spacing: 8, children: [
                if (booking.zoneId != null)
                  TextButton.icon(
                      icon: const Icon(Icons.map_outlined),
                      label: const Text('View space'),
                      onPressed: () => Navigator.push(
                          context,
                          MaterialPageRoute<void>(
                              builder: (_) => IndoorMapScreen(
                                  zoneId: booking.zoneId!,
                                  targetSlotId: booking.slotId,
                                  bookingId: booking.id)))),
                if (completed)
                  TextButton.icon(
                      icon: const Icon(Icons.receipt_long),
                      label: const Text('View receipt'),
                      onPressed: () => Navigator.push(
                          context,
                          MaterialPageRoute<void>(
                              builder: (_) =>
                                  ReceiptScreen(booking: booking)))),
                if (['Pending', 'Confirmed'].contains(booking.status))
                  TextButton(
                      onPressed: () async {
                        final confirmed = await showDialog<bool>(
                            context: context,
                            builder: (context) => AlertDialog(
                                    title:
                                        const Text('Cancel this reservation?'),
                                    content: Text(
                                        '${booking.vehiclePlate} · ${booking.zoneName ?? ''}'),
                                    actions: [
                                      TextButton(
                                          onPressed: () =>
                                              Navigator.pop(context, false),
                                          child:
                                              const Text('Keep reservation')),
                                      FilledButton(
                                          onPressed: () =>
                                              Navigator.pop(context, true),
                                          child:
                                              const Text('Cancel reservation'))
                                    ]));
                        if (confirmed != true) return;
                        final ok = await ref
                            .read(bookingProvider.notifier)
                            .cancelBooking(booking.id);
                        if (context.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(SnackBar(
                              content: Text(ok
                                  ? 'Reservation cancelled.'
                                  : ref.read(bookingProvider).error ??
                                      'Cancellation failed.')));
                        }
                      },
                      child: const Text('Cancel reservation')),
              ]),
            ])));
  }
}

class ReceiptScreen extends StatelessWidget {
  final BookingModel booking;
  const ReceiptScreen({super.key, required this.booking});
  @override
  Widget build(BuildContext context) {
    final session = booking.session!;
    final receipt =
        'OpenParking receipt\nReservation: ${booking.id}\nVehicle: ${booking.vehiclePlate}\n'
        '${booking.zoneName} · ${booking.slot?.slotNumber}\n'
        'Entry: ${session.checkInTime.toLocal()}\nExit: ${session.checkOutTime?.toLocal()}\n'
        'Parking fee: ${booking.currency} ${(session.totalFee - session.penaltyFee).toStringAsFixed(2)}\n'
        'Penalty: ${booking.currency} ${session.penaltyFee.toStringAsFixed(2)}\n'
        'Total: ${booking.currency} ${session.totalFee.toStringAsFixed(2)}\n'
        'Payment: ${booking.isPaid ? 'Received at gate' : 'Due at gate'}';
    return Scaffold(
        appBar: AppBar(title: const Text('Parking receipt')),
        body: Padding(
          padding: const EdgeInsets.all(24),
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            SelectableText(receipt),
            const SizedBox(height: 24),
            FilledButton.icon(
                icon: const Icon(Icons.copy),
                label: const Text('Copy receipt'),
                onPressed: () async {
                  await Clipboard.setData(ClipboardData(text: receipt));
                  if (context.mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Receipt copied.')));
                  }
                }),
          ]),
        ));
  }
}
