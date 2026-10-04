import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/auth_provider.dart';
import '../../core/providers/session_provider.dart';
import '../../core/providers/zone_provider.dart';
import '../../core/theme.dart';
import '../../core/widgets/app_widgets.dart';
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
        child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: pagePadding(context),
            children: [
              PageIntro(
                  title: 'Hello, ${user?.fullName ?? 'Driver'}',
                  subtitle: 'Your next parking space is a few taps away.'),
              session.when(
                loading: () => const LinearProgressIndicator(),
                error: (error, _) => ListTile(
                    title: const Text('Could not load your parking session'),
                    subtitle: Text('$error'),
                    trailing: IconButton(
                        icon: const Icon(Icons.refresh),
                        onPressed: () => ref
                            .read(activeSessionProvider.notifier)
                            .refresh())),
                data: (active) => Container(
                  padding: const EdgeInsets.all(24),
                  decoration: BoxDecoration(
                      color: AppTheme.primary,
                      borderRadius: BorderRadius.circular(12)),
                  child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(
                            active == null
                                ? Icons.local_parking
                                : Icons.directions_car,
                            color: Colors.white,
                            size: 28),
                        const SizedBox(height: 20),
                        Text(
                            active == null ? 'Ready to park?' : active.zoneName,
                            style: AppTheme.headlineSm.copyWith(
                                color: Colors.white,
                                fontWeight: FontWeight.w700)),
                        const SizedBox(height: 8),
                        Text(
                            active == null
                                ? 'Reserve a space. Give your vehicle number to the attendant at entry and exit.'
                                : active.status == 'OverstayDetected'
                                    ? 'Overstay detected. Please go to the exit gate.'
                                    : 'Parking session active · Space ${active.slotNumber}',
                            style: AppTheme.bodyMd.copyWith(
                                color: Colors.white.withValues(alpha: .8))),
                        const SizedBox(height: 24),
                        SizedBox(
                            width: double.infinity,
                            child: FilledButton.icon(
                              style: FilledButton.styleFrom(
                                  backgroundColor: Colors.white,
                                  foregroundColor: Colors.black),
                              icon: Icon(active == null
                                  ? Icons.search
                                  : Icons.arrow_forward),
                              label: Text(active == null
                                  ? 'Find Parking'
                                  : 'View My Bookings'),
                              onPressed: () => open(active == null
                                  ? const ZoneDiscoveryScreen()
                                  : const BookingScreen()),
                            )),
                      ]),
                ),
              ),
              const SizedBox(height: 24),
              LayoutBuilder(builder: (context, constraints) {
                final columns = constraints.maxWidth >= 600 ? 4 : 2;
                final width =
                    (constraints.maxWidth - (columns - 1) * 12) / columns;
                return Wrap(spacing: 12, runSpacing: 12, children: [
                  for (final action in <(IconData, String, Widget)>[
                    (Icons.search, 'Find Parking', const ZoneDiscoveryScreen()),
                    (
                      Icons.calendar_today_outlined,
                      'My Bookings',
                      const BookingScreen()
                    ),
                    (
                      Icons.shield_outlined,
                      'Penalties',
                      const PenaltiesScreen()
                    ),
                    (
                      Icons.accessible,
                      'Disability Permit',
                      const PermitUploadScreen()
                    ),
                  ])
                    SizedBox(
                        width: width,
                        child: QuickAction(
                            icon: action.$1,
                            label: action.$2,
                            onTap: () => open(action.$3))),
                ]);
              }),
              const SizedBox(height: 32),
              const Text('Parking zones', style: AppTheme.titleMd),
              const SizedBox(height: 12),
              if (zones.isLoading) const LinearProgressIndicator(),
              if (zones.error != null) Text(zones.error!),
              for (final zone in zones.zones.take(5))
                Card(
                    child: ListTile(
                  contentPadding:
                      const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  leading: const Icon(Icons.local_parking_outlined),
                  title: Text(zone.name, style: AppTheme.labelLg),
                  subtitle: Text(
                      '${zone.availableCount} available · ${zone.currency} ${zone.baseHourlyRate.toStringAsFixed(2)}/hour'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => open(
                      IndoorMapScreen(zoneId: zone.id, zoneName: zone.name)),
                )),
              TextButton(
                  onPressed: () => open(const ZoneDiscoveryScreen()),
                  child: const Text('View all parking zones')),
            ]),
      ),
    );
  }
}
