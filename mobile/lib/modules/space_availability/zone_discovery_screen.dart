import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/providers/zone_provider.dart';
import '../../core/theme.dart';
import '../../core/widgets/app_widgets.dart';
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
          ),
          SliverToBoxAdapter(
            child: Container(
              color: AppTheme.surfacePure,
              padding: pagePadding(context, vertical: 16),
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
                      contentPadding: const EdgeInsets.symmetric(vertical: 12),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(AppTheme.radiusXl),
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
              padding: pagePadding(context),
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
    final selected = ref.watch(zoneListProvider).filter == label;
    return FilterChip(
      selected: selected,
      showCheckmark: false,
      avatar: Icon(icon,
          size: 18,
          color: selected ? AppTheme.onPrimary : AppTheme.textSecondary),
      label: Text(label),
      labelStyle: AppTheme.labelMd.copyWith(
          color: selected ? AppTheme.onPrimary : AppTheme.textSecondary),
      materialTapTargetSize: MaterialTapTargetSize.padded,
      onSelected: (_) =>
          ref.read(zoneListProvider.notifier).fetchZones(filter: label),
    );
  }

  Widget _buildZoneCard(BuildContext context, ZoneModel zone) {
    final occupancy = zone.totalCapacity > 0
        ? ((zone.totalCapacity - zone.availableCount) / zone.totalCapacity)
            .clamp(0.0, 1.0)
        : 0.0;
    void open() => Navigator.push(
        context,
        MaterialPageRoute<void>(
            builder: (_) =>
                IndoorMapScreen(zoneId: zone.id, zoneName: zone.name)));
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: open,
        child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                    spacing: 12,
                    runSpacing: 10,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Text(zone.name, style: AppTheme.titleMd),
                      StatusBadge('${zone.availableCount} Available',
                          color: zone.availableCount > 0
                              ? AppTheme.accentSuccess
                              : AppTheme.accentCritical),
                    ]),
                const SizedBox(height: 8),
                Text('Code: ${zone.code}',
                    style:
                        AppTheme.bodySm.copyWith(color: AppTheme.textTertiary)),
                const SizedBox(height: 20),
                ClipRRect(
                    borderRadius: BorderRadius.circular(4),
                    child: LinearProgressIndicator(
                      value: occupancy,
                      minHeight: 4,
                      backgroundColor: AppTheme.surfaceMuted,
                      color: zone.availableCount > 0
                          ? AppTheme.primary
                          : AppTheme.accentCritical,
                    )),
                const SizedBox(height: 8),
                Text(
                    '${zone.availableCount} of ${zone.totalCapacity} spaces available',
                    style: AppTheme.bodySm
                        .copyWith(color: AppTheme.textSecondary)),
                const SizedBox(height: 16),
                Wrap(
                    spacing: 16,
                    runSpacing: 12,
                    alignment: WrapAlignment.spaceBetween,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Text(
                          '${zone.currency} ${zone.baseHourlyRate.toStringAsFixed(2)}/hr',
                          style: AppTheme.titleMd),
                      OutlinedButton.icon(
                          onPressed: open,
                          icon: const Icon(Icons.map_outlined, size: 18),
                          label: const Text('View Blueprint')),
                    ]),
              ],
            )),
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
