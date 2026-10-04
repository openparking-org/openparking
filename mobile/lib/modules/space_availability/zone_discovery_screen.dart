import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/providers/zone_provider.dart';
import '../../core/theme.dart';
import '../../models/zone.dart';
import 'indoor_map_screen.dart';

class ZoneDiscoveryScreen extends ConsumerStatefulWidget {
  const ZoneDiscoveryScreen({super.key});

  @override
  ConsumerState<ZoneDiscoveryScreen> createState() =>
      _ZoneDiscoveryScreenState();
}

class _ZoneDiscoveryScreenState extends ConsumerState<ZoneDiscoveryScreen> {
  final TextEditingController _searchController = TextEditingController();
  final ScrollController _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_onScroll);
  }

  void _onScroll() {
    if (_scrollController.position.pixels >=
        _scrollController.position.maxScrollExtent - 200) {
      ref.read(zoneListProvider.notifier).fetchZones(reset: false);
    }
  }

  @override
  void dispose() {
    _searchController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final zoneState = ref.watch(zoneListProvider);

    return Scaffold(
      backgroundColor: AppTheme.surface,
      body: CustomScrollView(
        controller: _scrollController,
        slivers: [
          // ── APP BAR ──
          SliverAppBar(
            floating: true,
            snap: true,
            backgroundColor: AppTheme.surfacePure,
            surfaceTintColor: Colors.transparent,
            toolbarHeight: 56,
            title: Text(
              'Find Parking',
              style: AppTheme.titleMd.copyWith(fontWeight: FontWeight.w700),
            ),
            actions: [
              IconButton(
                icon: const Icon(Icons.refresh, size: 20),
                color: AppTheme.primary,
                onPressed: () {
                  ref.read(zoneListProvider.notifier).fetchZones(reset: true);
                },
              ),
            ],
            bottom: PreferredSize(
              preferredSize: const Size.fromHeight(116),
              child: Container(
                color: AppTheme.surfacePure,
                padding: const EdgeInsets.fromLTRB(
                    AppTheme.margin, 0, AppTheme.margin, 12),
                child: Column(
                  children: [
                    // Search Field
                    TextField(
                      controller: _searchController,
                      onChanged: (value) {
                        ref
                            .read(zoneListProvider.notifier)
                            .fetchZones(query: value, reset: true);
                      },
                      style: AppTheme.bodyMd,
                      decoration: InputDecoration(
                        hintText: 'Search zone name or code...',
                        hintStyle: AppTheme.bodyMd
                            .copyWith(color: AppTheme.textTertiary),
                        prefixIcon: const Icon(Icons.search,
                            color: AppTheme.primary, size: 20),
                        suffixIcon: _searchController.text.isNotEmpty
                            ? IconButton(
                                icon: const Icon(Icons.clear,
                                    color: AppTheme.textSecondary, size: 18),
                                onPressed: () {
                                  _searchController.clear();
                                  ref
                                      .read(zoneListProvider.notifier)
                                      .fetchZones(query: '', reset: true);
                                },
                              )
                            : null,
                        filled: true,
                        fillColor: AppTheme.surfaceSubtle,
                        contentPadding:
                            const EdgeInsets.symmetric(vertical: 12),
                        border: OutlineInputBorder(
                          borderRadius:
                              BorderRadius.circular(AppTheme.radiusXl),
                          borderSide: BorderSide.none,
                        ),
                      ),
                    ),
                    const SizedBox(height: 12),
                    // Filter Chips
                    SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      child: Row(
                        children: [
                          _buildFilterChip('All', Icons.grid_view),
                          const SizedBox(width: 8),
                          _buildFilterChip(
                              'Available', Icons.check_circle_outline),
                          const SizedBox(width: 8),
                          _buildFilterChip('Disability', Icons.accessible),
                          const SizedBox(width: 8),
                          _buildFilterChip('EV Charging', Icons.ev_station),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),

          // ── ZONE LIST ──
          if (zoneState.isLoading)
            const SliverFillRemaining(
              hasScrollBody: false,
              child: Center(
                child: CircularProgressIndicator(
                  color: AppTheme.primary,
                  strokeWidth: 2,
                ),
              ),
            )
          else if (zoneState.error != null)
            SliverFillRemaining(
                hasScrollBody: false,
                child: Center(
                    child: Column(mainAxisSize: MainAxisSize.min, children: [
                  Text(zoneState.error!),
                  TextButton(
                      onPressed: () =>
                          ref.read(zoneListProvider.notifier).fetchZones(),
                      child: const Text('Retry')),
                ])))
          else if (zoneState.zones.isEmpty)
            SliverFillRemaining(hasScrollBody: false, child: _buildEmptyState())
          else
            SliverPadding(
              padding: const EdgeInsets.all(AppTheme.margin),
              sliver: SliverList(
                delegate: SliverChildBuilderDelegate(
                  (context, index) {
                    if (index == zoneState.zones.length) {
                      return const Padding(
                        padding: EdgeInsets.all(16),
                        child: Center(
                          child: CircularProgressIndicator(
                            color: AppTheme.primary,
                            strokeWidth: 2,
                          ),
                        ),
                      );
                    }
                    final zone = zoneState.zones[index];
                    return Padding(
                      padding: const EdgeInsets.only(bottom: AppTheme.spaceSm),
                      child: _buildZoneCard(context, zone),
                    );
                  },
                  childCount: zoneState.zones.length +
                      (zoneState.isLoadingMore ? 1 : 0),
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildFilterChip(String label, IconData icon) {
    final isSelected = ref.watch(zoneListProvider).filter == label;
    return GestureDetector(
      onTap: () {
        ref.read(zoneListProvider.notifier).fetchZones(filter: label);
      },
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        decoration: BoxDecoration(
          color: isSelected ? AppTheme.primary : AppTheme.surfaceSubtle,
          borderRadius: BorderRadius.circular(100),
          border: Border.all(
            color: isSelected ? AppTheme.primary : AppTheme.borderSubtle,
          ),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              icon,
              size: 16,
              color: isSelected ? AppTheme.onPrimary : AppTheme.textSecondary,
            ),
            const SizedBox(width: 6),
            Text(
              label,
              style: AppTheme.labelMd.copyWith(
                color: isSelected ? AppTheme.onPrimary : AppTheme.textSecondary,
                fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildZoneCard(BuildContext context, ZoneModel zone) {
    final occupancyRatio = zone.totalCapacity > 0
        ? ((zone.totalCapacity - zone.availableCount) / zone.totalCapacity)
            .clamp(0.0, 1.0)
        : 0.0;

    final Color statusColor = zone.availableCount == 0
        ? AppTheme.accentCritical
        : (occupancyRatio > 0.8
            ? AppTheme.accentCritical
            : AppTheme.accentSuccess);

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
          onTap: () {
            Navigator.push(
              context,
              MaterialPageRoute(
                builder: (context) => IndoorMapScreen(
                  zoneId: zone.id,
                  zoneName: zone.name,
                ),
              ),
            );
          },
          child: Padding(
            padding: const EdgeInsets.all(AppTheme.spaceMd),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
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
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Container(
                                      width: 6,
                                      height: 6,
                                      decoration: BoxDecoration(
                                        color: statusColor,
                                        shape: BoxShape.circle,
                                      ),
                                    ),
                                    const SizedBox(width: 4),
                                    Text(
                                      '${zone.availableCount} Available',
                                      style: AppTheme.labelSm.copyWith(
                                        color: statusColor,
                                        fontWeight: FontWeight.w700,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 4),
                          Text(
                            'Code: ${zone.code}',
                            style: AppTheme.bodySm.copyWith(
                              color: AppTheme.textSecondary,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),

                // Progress bar
                Row(
                  children: [
                    Expanded(
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(100),
                        child: LinearProgressIndicator(
                          value: occupancyRatio,
                          backgroundColor: AppTheme.surfaceSubtle,
                          valueColor:
                              AlwaysStoppedAnimation<Color>(statusColor),
                          minHeight: 6,
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Text(
                      '${zone.totalCapacity - zone.availableCount}/${zone.totalCapacity}',
                      style: AppTheme.bodySm.copyWith(
                        color: AppTheme.textSecondary,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),

                // Footer
                Container(
                  padding: const EdgeInsets.only(top: 10),
                  decoration: const BoxDecoration(
                    border: Border(
                      top: BorderSide(color: AppTheme.borderSubtle),
                    ),
                  ),
                  child: Wrap(
                    alignment: WrapAlignment.spaceBetween,
                    spacing: 12,
                    runSpacing: 8,
                    children: [
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(Icons.payments_outlined,
                              color: AppTheme.textSecondary, size: 16),
                          const SizedBox(width: 6),
                          Text(
                            '${zone.currency} ${zone.baseHourlyRate.toStringAsFixed(2)}/hr',
                            style: AppTheme.labelLg.copyWith(
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ],
                      ),
                      SizedBox(
                        height: 36,
                        child: ElevatedButton.icon(
                          onPressed: () {
                            Navigator.push(
                              context,
                              MaterialPageRoute(
                                builder: (context) => IndoorMapScreen(
                                  zoneId: zone.id,
                                  zoneName: zone.name,
                                ),
                              ),
                            );
                          },
                          icon: const Icon(Icons.map_outlined, size: 16),
                          label: Text(
                            'View Blueprint',
                            style: AppTheme.labelMd.copyWith(
                              color: AppTheme.onPrimary,
                            ),
                          ),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppTheme.primary,
                            foregroundColor: AppTheme.onPrimary,
                            elevation: 0,
                            padding: const EdgeInsets.symmetric(horizontal: 12),
                            shape: RoundedRectangleBorder(
                              borderRadius:
                                  BorderRadius.circular(AppTheme.radiusLg),
                            ),
                          ),
                        ),
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

  Widget _buildEmptyState() {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.location_off_outlined,
              size: 64, color: AppTheme.textTertiary.withValues(alpha: 0.4)),
          const SizedBox(height: 16),
          Text(
            'No Parking Lots Found',
            style: AppTheme.titleMd.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 8),
          Text(
            'Try adjusting your search query or filters',
            style: AppTheme.bodyMd.copyWith(color: AppTheme.textTertiary),
          ),
        ],
      ),
    );
  }
}
