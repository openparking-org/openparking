import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/theme.dart';
import '../../core/providers/session_provider.dart';
import '../../core/providers/zone_provider.dart';
import '../../services/session_service.dart';
import '../../models/zone.dart';
import '../booking/booking_screen.dart';

class HomeDashboardScreen extends ConsumerWidget {
  const HomeDashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sessionAsyncValue = ref.watch(activeSessionProvider);
    final activeSession = sessionAsyncValue.valueOrNull;
    final zoneState = ref.watch(zoneListProvider);

    return Scaffold(
      backgroundColor: AppTheme.surface,
      body: CustomScrollView(
        slivers: [
          // ── TOP APP BAR ──
          SliverAppBar(
            floating: true,
            snap: true,
            backgroundColor: AppTheme.surfacePure,
            surfaceTintColor: Colors.transparent,
            elevation: 0,
            toolbarHeight: 56,
            title: Row(
              children: [
                Container(
                  width: 32,
                  height: 32,
                  decoration: BoxDecoration(
                    color: AppTheme.primary,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: const Icon(
                    Icons.local_parking,
                    color: AppTheme.surfacePure,
                    size: 18,
                  ),
                ),
                const SizedBox(width: AppTheme.spaceSm),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'OpenParking',
                      style: AppTheme.titleMd.copyWith(
                        fontWeight: FontWeight.w700,
                        letterSpacing: -0.3,
                      ),
                    ),
                    Row(
                      children: [
                        const Icon(
                          Icons.location_on,
                          size: 13,
                          color: AppTheme.textTertiary,
                        ),
                        const SizedBox(width: 2),
                        Text(
                          'Financial District, SF',
                          style: AppTheme.labelSm.copyWith(
                            color: AppTheme.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ],
            ),
            actions: [
              Stack(
                children: [
                  IconButton(
                    icon: const Icon(Icons.notifications_outlined, size: 22),
                    color: AppTheme.primary,
                    onPressed: () {},
                  ),
                  Positioned(
                    right: 10,
                    top: 10,
                    child: Container(
                      width: 8,
                      height: 8,
                      decoration: BoxDecoration(
                        color: AppTheme.accentCritical,
                        shape: BoxShape.circle,
                        border: Border.all(
                          color: AppTheme.surfacePure,
                          width: 1.5,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(width: 4),
            ],
          ),

          // ── MAIN CONTENT ──
          SliverPadding(
            padding: const EdgeInsets.symmetric(horizontal: AppTheme.margin),
            sliver: SliverList(
              delegate: SliverChildListDelegate([
                const SizedBox(height: AppTheme.spaceMd),

                // ── HERO: ACTIVE SESSION CARD ──
                if (activeSession != null)
                  _ActiveSessionHero(session: activeSession)
                else
                  _NoActiveSessionCard(),

                const SizedBox(height: AppTheme.spaceMd),

                // ── QUICK ACTIONS GRID ──
                _QuickActionsGrid(),

                const SizedBox(height: AppTheme.spaceMd + 8),

                // ── NEARBY PARKING ZONES ──
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Nearby Parking Zones',
                          style: AppTheme.titleMd.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          'Real-time availability',
                          style: AppTheme.bodySm.copyWith(
                            color: AppTheme.textSecondary,
                          ),
                        ),
                      ],
                    ),
                    TextButton(
                      onPressed: () {},
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            'View All',
                            style: AppTheme.labelMd.copyWith(
                              color: AppTheme.primary,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(width: 2),
                          const Icon(Icons.arrow_forward, size: 16),
                        ],
                      ),
                    ),
                  ],
                ),

                const SizedBox(height: AppTheme.spaceSm),

                // Zone Cards from API
                if (zoneState.isLoading)
                  const Padding(
                    padding: EdgeInsets.all(32),
                    child: Center(
                      child: CircularProgressIndicator(
                        color: AppTheme.primary,
                        strokeWidth: 2,
                      ),
                    ),
                  )
                else if (zoneState.zones.isEmpty)
                  _buildEmptyZonesState()
                else
                  ...zoneState.zones.take(5).map((zone) => Padding(
                        padding:
                            const EdgeInsets.only(bottom: AppTheme.spaceSm),
                        child: _NearbyZoneCard(zone: zone),
                      )),

                const SizedBox(height: 100), // Bottom nav padding
              ]),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildEmptyZonesState() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(32),
      decoration: BoxDecoration(
        color: AppTheme.surfacePure,
        borderRadius: BorderRadius.circular(AppTheme.radiusXl),
        border: Border.all(color: AppTheme.borderSubtle),
      ),
      child: Column(
        children: [
          Icon(Icons.location_off_outlined,
              size: 48, color: AppTheme.textTertiary.withValues(alpha: 0.5)),
          const SizedBox(height: 12),
          Text(
            'No Nearby Zones Found',
            style: AppTheme.labelLg.copyWith(color: AppTheme.textPrimary),
          ),
          const SizedBox(height: 4),
          Text(
            'Pull to refresh or check your connection',
            style: AppTheme.bodySm.copyWith(color: AppTheme.textTertiary),
          ),
        ],
      ),
    );
  }
}

// ── HERO: ACTIVE SESSION ────────────────────────────────────────
class _ActiveSessionHero extends StatelessWidget {
  final ParkingSessionModel session;

  const _ActiveSessionHero({required this.session});

  String _formatDuration(DateTime checkInTime) {
    final diff = DateTime.now().difference(checkInTime);
    final hours = diff.inHours.toString().padLeft(2, '0');
    final minutes = (diff.inMinutes % 60).toString().padLeft(2, '0');
    return '${hours}h ${minutes}m';
  }

  double _elapsedFraction(DateTime checkInTime) {
    final diff = DateTime.now().difference(checkInTime).inMinutes;
    // Assume a 3-hour session by default
    return (diff / 180.0).clamp(0.0, 1.0);
  }

  @override
  Widget build(BuildContext context) {
    final progress = _elapsedFraction(session.checkInTime);
    final progressPercent = (progress * 100).toInt();

    return Container(
      decoration: BoxDecoration(
        color: AppTheme.primary,
        borderRadius: BorderRadius.circular(AppTheme.radiusXl),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.12),
            blurRadius: 24,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Stack(
        children: [
          // Decorative circles
          Positioned(
            right: -32,
            top: -32,
            child: Container(
              width: 144,
              height: 144,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(
                  color: Colors.white.withValues(alpha: 0.1),
                ),
              ),
            ),
          ),
          Positioned(
            right: -8,
            top: -8,
            child: Container(
              width: 96,
              height: 96,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(
                  color: Colors.white.withValues(alpha: 0.15),
                ),
              ),
            ),
          ),

          // Content
          Padding(
            padding: const EdgeInsets.all(AppTheme.spaceMd),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Status pill
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: Colors.white.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(100),
                        border: Border.all(
                          color: Colors.white.withValues(alpha: 0.2),
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
                            'CURRENT SESSION',
                            style: AppTheme.labelSm.copyWith(
                              color: Colors.white,
                              letterSpacing: 0.8,
                            ),
                          ),
                        ],
                      ),
                    ),
                    Text(
                      'Active',
                      style: AppTheme.labelSm.copyWith(
                        color: AppTheme.surfaceContainerHigh,
                      ),
                    ),
                  ],
                ),

                const SizedBox(height: 12),

                // Zone name
                Text(
                  session.zoneName,
                  style: AppTheme.titleMd.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  'Bay ${session.slotNumber}',
                  style: AppTheme.bodySm.copyWith(
                    color: AppTheme.surfaceContainerHigh,
                  ),
                ),

                const SizedBox(height: 8),

                // Vehicle info
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.05),
                    borderRadius: BorderRadius.circular(4),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(
                        Icons.directions_car,
                        size: 14,
                        color: AppTheme.surfaceDim,
                      ),
                      const SizedBox(width: 6),
                      Text(
                        'Parked Vehicle',
                        style: AppTheme.labelSm.copyWith(
                          color: AppTheme.surfaceDim,
                        ),
                      ),
                    ],
                  ),
                ),

                // Countdown section
                Container(
                  margin: const EdgeInsets.only(top: 16),
                  padding: const EdgeInsets.only(top: 12),
                  decoration: BoxDecoration(
                    border: Border(
                      top: BorderSide(
                        color: Colors.white.withValues(alpha: 0.15),
                      ),
                    ),
                  ),
                  child: Column(
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'ELAPSED TIME',
                                style: AppTheme.labelSm.copyWith(
                                  color: AppTheme.surfaceContainerHigh,
                                  letterSpacing: 0.8,
                                ),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                _formatDuration(session.checkInTime),
                                style: AppTheme.headlineMd.copyWith(
                                  color: Colors.white,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ],
                          ),
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.end,
                            children: [
                              Text(
                                'Rate',
                                style: AppTheme.labelSm.copyWith(
                                  color: AppTheme.surfaceContainerHigh,
                                ),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                '\$${session.hourlyRate.toStringAsFixed(2)}/hr',
                                style: AppTheme.bodyMd.copyWith(
                                  color: Colors.white,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),

                      const SizedBox(height: 10),

                      // Progress bar
                      ClipRRect(
                        borderRadius: BorderRadius.circular(100),
                        child: LinearProgressIndicator(
                          value: progress,
                          backgroundColor: Colors.white.withValues(alpha: 0.2),
                          valueColor:
                              const AlwaysStoppedAnimation<Color>(Colors.white),
                          minHeight: 6,
                        ),
                      ),
                      const SizedBox(height: 6),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(
                            'Progress ($progressPercent% elapsed)',
                            style: AppTheme.labelSm.copyWith(
                              color: AppTheme.surfaceContainerHigh,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),

                const SizedBox(height: 16),

                // Actions
                Row(
                  children: [
                    Expanded(
                      child: SizedBox(
                        height: 40,
                        child: ElevatedButton(
                          onPressed: () {
                            Navigator.push(
                              context,
                              MaterialPageRoute(
                                builder: (_) => const BookingScreen(),
                              ),
                            );
                          },
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppTheme.surfacePure,
                            foregroundColor: AppTheme.primary,
                            elevation: 0,
                            shape: RoundedRectangleBorder(
                              borderRadius:
                                  BorderRadius.circular(AppTheme.radiusXl),
                            ),
                          ),
                          child: Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              const Icon(Icons.tune, size: 18),
                              const SizedBox(width: 6),
                              Text(
                                'Manage Session',
                                style: AppTheme.labelLg.copyWith(
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    SizedBox(
                      height: 40,
                      width: 40,
                      child: OutlinedButton(
                        onPressed: () {},
                        style: OutlinedButton.styleFrom(
                          padding: EdgeInsets.zero,
                          backgroundColor: Colors.white.withValues(alpha: 0.1),
                          side: BorderSide(
                            color: Colors.white.withValues(alpha: 0.2),
                          ),
                          shape: RoundedRectangleBorder(
                            borderRadius:
                                BorderRadius.circular(AppTheme.radiusXl),
                          ),
                        ),
                        child: const Icon(
                          Icons.more_time,
                          color: Colors.white,
                          size: 18,
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// ── NO ACTIVE SESSION CARD ──────────────────────────────────────
class _NoActiveSessionCard extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppTheme.primary,
        borderRadius: BorderRadius.circular(AppTheme.radiusXl),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.12),
            blurRadius: 24,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      padding: const EdgeInsets.all(AppTheme.spaceMd),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(100),
              border: Border.all(
                color: Colors.white.withValues(alpha: 0.2),
              ),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  width: 6,
                  height: 6,
                  decoration: const BoxDecoration(
                    color: AppTheme.surfaceMuted,
                    shape: BoxShape.circle,
                  ),
                ),
                const SizedBox(width: 6),
                Text(
                  'NO ACTIVE SESSION',
                  style: AppTheme.labelSm.copyWith(
                    color: Colors.white,
                    letterSpacing: 0.8,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          Text(
            'Ready to Park?',
            style: AppTheme.titleMd.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Find nearby parking or scan a gate QR to start your session.',
            style: AppTheme.bodySm.copyWith(
              color: AppTheme.surfaceContainerHigh,
            ),
          ),
          const SizedBox(height: 16),
          SizedBox(
            height: 40,
            child: ElevatedButton(
              onPressed: () {},
              style: ElevatedButton.styleFrom(
                backgroundColor: AppTheme.surfacePure,
                foregroundColor: AppTheme.primary,
                elevation: 0,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(AppTheme.radiusXl),
                ),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.search, size: 18),
                  const SizedBox(width: 6),
                  Text(
                    'Find Parking',
                    style: AppTheme.labelLg.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ── QUICK ACTIONS GRID ──────────────────────────────────────────
class _QuickActionsGrid extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        _QuickActionCard(
          icon: Icons.map_outlined,
          label: 'Find Parking',
          subtitle: 'Live spaces',
          onTap: () {},
        ),
        const SizedBox(width: AppTheme.spaceSm),
        _QuickActionCard(
          icon: Icons.qr_code_scanner,
          label: 'Scan Gate QR',
          subtitle: 'Quick entry',
          onTap: () {},
        ),
        const SizedBox(width: AppTheme.spaceSm),
        _QuickActionCard(
          icon: Icons.shield_outlined,
          label: 'Penalties',
          subtitle: '0 Due',
          subtitleColor: AppTheme.accentSuccess,
          onTap: () {},
        ),
      ],
    );
  }
}

class _QuickActionCard extends StatelessWidget {
  final IconData icon;
  final String label;
  final String subtitle;
  final Color? subtitleColor;
  final VoidCallback onTap;

  const _QuickActionCard({
    required this.icon,
    required this.label,
    required this.subtitle,
    this.subtitleColor,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          constraints: const BoxConstraints(minHeight: 96),
          padding: const EdgeInsets.all(AppTheme.spaceSm),
          decoration: BoxDecoration(
            color: AppTheme.surfacePure,
            borderRadius: BorderRadius.circular(AppTheme.radiusXl),
            border: Border.all(color: AppTheme.borderSubtle),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: const BoxDecoration(
                  color: AppTheme.surfaceSubtle,
                  shape: BoxShape.circle,
                ),
                child: Icon(icon, size: 20, color: AppTheme.primary),
              ),
              const SizedBox(height: 6),
              Text(
                label,
                style: AppTheme.labelMd.copyWith(
                  fontWeight: FontWeight.w700,
                ),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 2),
              if (subtitleColor != null)
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                  decoration: BoxDecoration(
                    color: AppTheme.surfaceSubtle,
                    borderRadius: BorderRadius.circular(100),
                    border: Border.all(color: AppTheme.borderSubtle),
                  ),
                  child: Text(
                    subtitle,
                    style: AppTheme.labelSm.copyWith(
                      color: subtitleColor,
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                )
              else
                Text(
                  subtitle,
                  style: AppTheme.labelSm.copyWith(
                    color: AppTheme.textTertiary,
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

// ── NEARBY ZONE CARD ────────────────────────────────────────────
class _NearbyZoneCard extends StatelessWidget {
  final ZoneModel zone;

  const _NearbyZoneCard({required this.zone});

  @override
  Widget build(BuildContext context) {
    final isAlmostFull = zone.availableCount <= 5 && zone.availableCount > 0;
    final isFull = zone.availableCount == 0;

    Color statusColor = AppTheme.accentSuccess;
    String statusText = '${zone.availableCount} spots open';
    if (isFull) {
      statusColor = AppTheme.accentCritical;
      statusText = 'Full';
    } else if (isAlmostFull) {
      statusColor = AppTheme.accentCritical;
      statusText = 'Almost Full (${zone.availableCount} spots)';
    }

    return Container(
      decoration: BoxDecoration(
        color: AppTheme.surfacePure,
        borderRadius: BorderRadius.circular(AppTheme.radiusXl),
        border: Border.all(color: AppTheme.borderSubtle),
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: BorderRadius.circular(AppTheme.radiusXl),
          onTap: () {},
          child: Padding(
            padding: const EdgeInsets.all(AppTheme.spaceMd),
            child: Column(
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Flexible(
                                child: Text(
                                  zone.name,
                                  style: AppTheme.bodyLg.copyWith(
                                    fontWeight: FontWeight.w700,
                                    color: AppTheme.primary,
                                  ),
                                ),
                              ),
                              const SizedBox(width: 8),
                              Container(
                                padding: const EdgeInsets.symmetric(
                                    horizontal: 8, vertical: 3),
                                decoration: BoxDecoration(
                                  color: AppTheme.surfaceSubtle,
                                  borderRadius: BorderRadius.circular(100),
                                  border:
                                      Border.all(color: AppTheme.borderSubtle),
                                ),
                                child: Text(
                                  statusText,
                                  style: AppTheme.labelSm.copyWith(
                                    color: statusColor,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 4),
                          Row(
                            children: [
                              const Icon(Icons.navigation,
                                  size: 14, color: AppTheme.textSecondary),
                              const SizedBox(width: 4),
                              Text(
                                'Code: ${zone.code}',
                                style: AppTheme.bodySm.copyWith(
                                  color: AppTheme.textSecondary,
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Text(
                          '${zone.currency} ${zone.baseHourlyRate.toStringAsFixed(2)}',
                          style: AppTheme.titleMd.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        Text(
                          '/hr',
                          style: AppTheme.labelSm.copyWith(
                            color: AppTheme.textTertiary,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),

                // Divider & footer
                Container(
                  margin: const EdgeInsets.only(top: 12),
                  padding: const EdgeInsets.only(top: 10),
                  decoration: const BoxDecoration(
                    border: Border(
                      top: BorderSide(color: AppTheme.borderSubtle),
                    ),
                  ),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Row(
                        children: [
                          Icon(
                            zone.availableCount > 20
                                ? Icons.electric_car
                                : Icons.roofing,
                            size: 15,
                            color: zone.availableCount > 20
                                ? AppTheme.accentSuccess
                                : AppTheme.textSecondary,
                          ),
                          const SizedBox(width: 6),
                          Text(
                            zone.availableCount > 20
                                ? 'EV Charging available'
                                : 'Covered Parking',
                            style: AppTheme.labelSm.copyWith(
                              color: AppTheme.textSecondary,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ],
                      ),
                      Row(
                        children: [
                          Text(
                            'Reserve',
                            style: AppTheme.labelSm.copyWith(
                              color: AppTheme.primary,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(width: 2),
                          const Icon(Icons.chevron_right,
                              size: 14, color: AppTheme.primary),
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
