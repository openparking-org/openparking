import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/providers/zone_provider.dart';
import '../../core/theme.dart';
import '../../core/widgets/app_widgets.dart';
import '../../models/zone.dart';
import '../../services/parking_recommendation_service.dart';
import 'zone_map_view.dart';
import 'indoor_map_screen.dart';

class ZoneDiscoveryScreen extends ConsumerStatefulWidget {
  final bool autoRecommend;
  const ZoneDiscoveryScreen({super.key, this.autoRecommend = false});

  @override
  ConsumerState<ZoneDiscoveryScreen> createState() =>
      _ZoneDiscoveryScreenState();
}

class _ZoneDiscoveryScreenState extends ConsumerState<ZoneDiscoveryScreen> {
  final TextEditingController _searchController = TextEditingController();
  final ScrollController _scrollController = ScrollController();
  bool _finding = false;
  String? _recommendationError;
  ParkingRecommendations? _recommendations;
  ParkingLocation? _location;
  String _preference = 'nearest';
  double _radius = 25;
  String _slotType = 'Standard';
  bool _showMap = false;

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_onScroll);
    if (widget.autoRecommend) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _findParking();
      });
    }
  }

  Future<void> _findParking() async {
    if (_finding) return;
    setState(() {
      _finding = true;
      _recommendationError = null;
      _recommendations = null;
    });
    try {
      final location = await ref.read(parkingLocationProvider).current();
      if (!mounted) return;
      final results = await ref.read(parkingRecommendationProvider).find(
          location,
          preference: _preference,
          radiusKm: _radius,
          slotType: _slotType);
      if (!mounted) return;
      setState(() {
        _location = location;
        _recommendations = results;
      });
    } catch (error) {
      if (mounted) {
        setState(() {
          _recommendationError =
              error.toString().replaceFirst('Exception: ', '');
        });
      }
    } finally {
      if (mounted) setState(() => _finding = false);
    }
  }

  Widget _recommendationPanel() {
    return Padding(
      padding: pagePadding(context),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const Text('Recommended parking', style: AppTheme.titleMd),
        const SizedBox(height: 8),
        const Text('Use your location to find available spaces nearby.'),
        const SizedBox(height: 8),
        Wrap(spacing: 12, runSpacing: 8, children: [
          DropdownButton<String>(
              value: _preference,
              items: const [
                DropdownMenuItem(value: 'nearest', child: Text('Nearest')),
                DropdownMenuItem(value: 'cheapest', child: Text('Cheapest')),
                DropdownMenuItem(value: 'balanced', child: Text('Balanced'))
              ],
              onChanged: _finding
                  ? null
                  : (v) => setState(() {
                        _preference = v!;
                        _recommendations = null;
                      })),
          DropdownButton<double>(
              value: _radius,
              items: const [
                DropdownMenuItem(value: 5, child: Text('Within 5 km')),
                DropdownMenuItem(value: 25, child: Text('Within 25 km')),
                DropdownMenuItem(value: 100, child: Text('Within 100 km'))
              ],
              onChanged: _finding
                  ? null
                  : (v) => setState(() {
                        _radius = v!;
                        _recommendations = null;
                      })),
          DropdownButton<String>(
              value: _slotType,
              items: const [
                DropdownMenuItem(value: 'Standard', child: Text('Standard')),
                DropdownMenuItem(value: 'EV', child: Text('EV charging')),
                DropdownMenuItem(value: 'Accessible', child: Text('Accessible'))
              ],
              onChanged: _finding
                  ? null
                  : (v) => setState(() {
                        _slotType = v!;
                        _recommendations = null;
                      })),
        ]),
        FilledButton.icon(
            onPressed: _finding ? null : _findParking,
            icon: const Icon(Icons.my_location),
            label: Text(
                _finding ? 'Finding nearby parking…' : 'Find parking near me')),
        if (_finding)
          const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: LinearProgressIndicator()),
        if (_recommendationError != null)
          Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Text(_recommendationError!,
                  style: const TextStyle(color: AppTheme.accentCritical))),
        if (_recommendations != null) ...[
          const SizedBox(height: 12),
          Text(
              'Parking agent completed • location accuracy ±${_location!.accuracy.round()} m',
              style: AppTheme.bodySm),
          const Text(
              'Distances are straight-line estimates, not driving distances.',
              style: AppTheme.bodySm),
          Text(_recommendations!.message, style: AppTheme.bodySm),
          for (final item in _recommendations!.items)
            Card(
              child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(item.name, style: AppTheme.titleMd),
                        Text(
                            '${(item.distanceMeters / 1000).toStringAsFixed(2)} km • ${item.currency} ${item.hourlyRate.toStringAsFixed(2)}/hr'),
                        Text(item.reason),
                        const SizedBox(height: 8),
                        OutlinedButton.icon(
                            onPressed: () => Navigator.push(
                                context,
                                MaterialPageRoute<void>(
                                    builder: (_) => IndoorMapScreen(
                                        zoneId: item.zoneId,
                                        recommendedSlotType: _slotType,
                                        zoneName: item.name))),
                            icon: const Icon(Icons.local_parking),
                            label: const Text('Choose a space')),
                      ])),
            ),
        ],
        const SizedBox(height: 16),
        const Text('Browse all parking zones', style: AppTheme.titleMd),
      ]),
    );
  }

  void _onScroll() {
    if (!_showMap && _scrollController.hasClients && _scrollController.position.pixels >=
        _scrollController.position.maxScrollExtent - 200) {
      ref.read(zoneListProvider.notifier).fetchZones(reset: false);
    }
  }

  @override
  void didUpdateWidget(covariant ZoneDiscoveryScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.autoRecommend &&
        !oldWidget.autoRecommend &&
        _recommendations == null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _findParking();
      });
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
      body: _showMap
          ? Column(
              children: [
                AppBar(
                  backgroundColor: AppTheme.surfacePure,
                  title: Text('Find Parking', style: AppTheme.titleMd.copyWith(fontWeight: FontWeight.w700)),
                  actions: [
                    IconButton(
                      icon: const Icon(Icons.list, color: AppTheme.primary),
                      onPressed: () => setState(() => _showMap = false),
                    )
                  ],
                ),
                const Expanded(child: ZoneMapView()),
              ],
            )
          : CustomScrollView(
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
                icon: const Icon(Icons.map, size: 20, color: AppTheme.primary),
                onPressed: () => setState(() => _showMap = true),
              ),
              IconButton(
                icon: const Icon(Icons.refresh, size: 20),
                color: AppTheme.primary,
                onPressed: () {
                  ref.read(zoneListProvider.notifier).fetchZones(reset: true);
                  _findParking();
                },
              ),
            ],
          ),
          SliverToBoxAdapter(child: _recommendationPanel()),
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
