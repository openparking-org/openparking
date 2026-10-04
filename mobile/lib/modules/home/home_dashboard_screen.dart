import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/auth_provider.dart';
import '../../core/providers/session_provider.dart';
import '../../core/providers/zone_provider.dart';
import '../../core/theme.dart';
import '../booking/booking_screen.dart';
import '../enforcement/penalties_screen.dart';
import '../space_availability/zone_discovery_screen.dart';
import '../space_availability/indoor_map_screen.dart';
import '../user_access/permit_upload_screen.dart';

class HomeDashboardScreen extends ConsumerWidget {
  const HomeDashboardScreen({super.key});
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(authProvider).user;
    final session = ref.watch(activeSessionProvider);
    final zones = ref.watch(zoneListProvider);
    void open(Widget screen) => Navigator.push(
        context, MaterialPageRoute<void>(builder: (_) => screen));
    return Scaffold(
      appBar: AppBar(title: const Text('OpenParking')),
      body: RefreshIndicator(
        onRefresh: () async {
          await Future.wait([
            ref.read(activeSessionProvider.notifier).refresh(),
            ref.read(zoneListProvider.notifier).fetchZones()
          ]);
        },
        child:
            ListView(padding: const EdgeInsets.all(AppTheme.margin), children: [
          Text('Hello, ${user?.fullName ?? 'Driver'}', style: AppTheme.titleMd),
          const SizedBox(height: 12),
          const Text(
              'Reserve a space, then give your vehicle number to the attendant at entry and exit.'),
          const SizedBox(height: 20),
          session.when(
            loading: () => const LinearProgressIndicator(),
            error: (error, _) => ListTile(
                title: const Text('Could not load your parking session'),
                subtitle: Text('$error'),
                trailing: IconButton(
                    icon: const Icon(Icons.refresh),
                    onPressed: () =>
                        ref.read(activeSessionProvider.notifier).refresh())),
            data: (active) => Card(
                child: ListTile(
              leading: Icon(
                  active == null ? Icons.local_parking : Icons.directions_car),
              title: Text(active == null
                  ? 'Ready to park?'
                  : '${active.zoneName} · ${active.slotNumber}'),
              subtitle: Text(active == null
                  ? 'Find a space and reserve it.'
                  : active.status == 'OverstayDetected'
                      ? 'Overstay detected. Please go to the exit gate.'
                      : 'Parking session active'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => open(active == null
                  ? const ZoneDiscoveryScreen()
                  : const BookingScreen()),
            )),
          ),
          const SizedBox(height: 16),
          Wrap(spacing: 8, runSpacing: 8, children: [
            ActionChip(
                avatar: const Icon(Icons.search),
                label: const Text('Find Parking'),
                onPressed: () => open(const ZoneDiscoveryScreen())),
            ActionChip(
                avatar: const Icon(Icons.calendar_today),
                label: const Text('My Bookings'),
                onPressed: () => open(const BookingScreen())),
            ActionChip(
                avatar: const Icon(Icons.shield_outlined),
                label: const Text('Penalties'),
                onPressed: () => open(const PenaltiesScreen())),
            ActionChip(
                avatar: const Icon(Icons.accessible),
                label: const Text('Disability Permit'),
                onPressed: () => open(const PermitUploadScreen())),
          ]),
          const SizedBox(height: 24),
          const Text('Parking zones', style: AppTheme.titleMd),
          if (zones.isLoading) const LinearProgressIndicator(),
          if (zones.error != null) Text(zones.error!),
          for (final zone in zones.zones.take(5))
            Card(
                child: ListTile(
              title: Text(zone.name),
              subtitle: Text(
                  '${zone.availableCount} available · ${zone.currency} ${zone.baseHourlyRate.toStringAsFixed(2)}/hour'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () =>
                  open(IndoorMapScreen(zoneId: zone.id, zoneName: zone.name)),
            )),
          TextButton(
              onPressed: () => open(const ZoneDiscoveryScreen()),
              child: const Text('View all parking zones')),
        ]),
      ),
    );
  }
}
