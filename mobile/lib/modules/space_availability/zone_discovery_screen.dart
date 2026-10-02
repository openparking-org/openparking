import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/providers/zone_provider.dart';
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
  String _selectedFilter = 'All';

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
      backgroundColor: const Color(0xFF0B0F19),
      appBar: AppBar(
        title: const Text(
          'Discover Parking Lots',
          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 20),
        ),
        backgroundColor: const Color(0xFF111827),
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () {
              ref.read(zoneListProvider.notifier).fetchZones(reset: true);
            },
          )
        ],
      ),
      body: Column(
        children: [
          // Search & Filter Header
          Container(
            padding: const EdgeInsets.all(16),
            color: const Color(0xFF111827),
            child: Column(
              children: [
                TextField(
                  controller: _searchController,
                  onChanged: (value) {
                    ref
                        .read(zoneListProvider.notifier)
                        .fetchZones(query: value, reset: true);
                  },
                  decoration: InputDecoration(
                    hintText: 'Search zone name or code...',
                    hintStyle: const TextStyle(color: Colors.white38),
                    prefixIcon:
                        const Icon(Icons.search, color: Color(0xFF6366F1)),
                    suffixIcon: _searchController.text.isNotEmpty
                        ? IconButton(
                            icon:
                                const Icon(Icons.clear, color: Colors.white54),
                            onPressed: () {
                              _searchController.clear();
                              ref
                                  .read(zoneListProvider.notifier)
                                  .fetchZones(query: '', reset: true);
                            },
                          )
                        : null,
                    filled: true,
                    fillColor: const Color(0xFF1E293B),
                    contentPadding: const EdgeInsets.symmetric(vertical: 12),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(12),
                      borderSide: BorderSide.none,
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(
                    children: [
                      _buildFilterChip('All', Icons.grid_view),
                      const SizedBox(width: 8),
                      _buildFilterChip('Available', Icons.check_circle_outline),
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

          // Zone List Body
          Expanded(
            child: RefreshIndicator(
              onRefresh: () async {
                await ref
                    .read(zoneListProvider.notifier)
                    .fetchZones(reset: true);
              },
              color: const Color(0xFF6366F1),
              backgroundColor: const Color(0xFF1E293B),
              child: zoneState.isLoading
                  ? const Center(
                      child:
                          CircularProgressIndicator(color: Color(0xFF6366F1)))
                  : zoneState.zones.isEmpty
                      ? _buildEmptyState()
                      : ListView.builder(
                          controller: _scrollController,
                          padding: const EdgeInsets.all(16),
                          itemCount: zoneState.zones.length +
                              (zoneState.isLoadingMore ? 1 : 0),
                          itemBuilder: (context, index) {
                            if (index == zoneState.zones.length) {
                              return const Padding(
                                padding: EdgeInsets.all(16),
                                child: Center(
                                  child: CircularProgressIndicator(
                                      color: Color(0xFF6366F1)),
                                ),
                              );
                            }

                            final zone = zoneState.zones[index];
                            return _buildZoneCard(context, zone);
                          },
                        ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildFilterChip(String label, IconData icon) {
    final isSelected = _selectedFilter == label;
    return ChoiceChip(
      avatar: Icon(
        icon,
        size: 16,
        color: isSelected ? Colors.white : Colors.white60,
      ),
      label: Text(label),
      selected: isSelected,
      selectedColor: const Color(0xFF6366F1),
      backgroundColor: const Color(0xFF1E293B),
      labelStyle: TextStyle(
        color: isSelected ? Colors.white : Colors.white70,
        fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
      ),
      onSelected: (selected) {
        if (selected) {
          setState(() => _selectedFilter = label);
        }
      },
    );
  }

  Widget _buildZoneCard(BuildContext context, ZoneModel zone) {
    final occupancyRatio = zone.totalCapacity > 0
        ? (zone.totalCapacity - zone.availableCount) / zone.totalCapacity
        : 0.0;

    final Color statusColor = zone.availableCount == 0
        ? Colors.redAccent
        : (occupancyRatio > 0.8
            ? Colors.orangeAccent
            : const Color(0xFF10B981));

    return Card(
      margin: const EdgeInsets.only(bottom: 16),
      color: const Color(0xFF1E293B),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      elevation: 4,
      child: Padding(
        padding: const EdgeInsets.all(16),
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
                      Text(
                        zone.name,
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                          color: Colors.white,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Code: ${zone.code}',
                        style: const TextStyle(
                            color: Colors.white54, fontSize: 13),
                      ),
                    ],
                  ),
                ),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(
                    color: statusColor.withValues(alpha: 0.2),
                    borderRadius: BorderRadius.circular(20),
                    border:
                        Border.all(color: statusColor.withValues(alpha: 0.5)),
                  ),
                  child: Row(
                    children: [
                      Container(
                        width: 8,
                        height: 8,
                        decoration: BoxDecoration(
                            color: statusColor, shape: BoxShape.circle),
                      ),
                      const SizedBox(width: 6),
                      Text(
                        '${zone.availableCount} Available',
                        style: TextStyle(
                          color: statusColor,
                          fontWeight: FontWeight.bold,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),

            // Capacity Progress Bar
            Row(
              children: [
                Expanded(
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(4),
                    child: LinearProgressIndicator(
                      value: occupancyRatio,
                      backgroundColor: const Color(0xFF0B0F19),
                      color: statusColor,
                      minHeight: 6,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Text(
                  '${zone.totalCapacity - zone.availableCount}/${zone.totalCapacity} Slots',
                  style: const TextStyle(color: Colors.white60, fontSize: 12),
                ),
              ],
            ),
            const SizedBox(height: 16),

            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    const Icon(Icons.payments_outlined,
                        color: Colors.white70, size: 18),
                    const SizedBox(width: 6),
                    Text(
                      '${zone.currency} ${zone.baseHourlyRate.toStringAsFixed(2)}/hr',
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 15,
                      ),
                    ),
                  ],
                ),
                ElevatedButton.icon(
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
                  icon: const Icon(Icons.map_outlined, size: 18),
                  label: const Text('View Blueprint'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF6366F1),
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildEmptyState() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.location_off_outlined,
              size: 64, color: Colors.white.withValues(alpha: 0.3)),
          const SizedBox(height: 16),
          const Text(
            'No Parking Lots Found',
            style: TextStyle(
                fontSize: 18,
                color: Colors.white70,
                fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 8),
          const Text(
            'Try adjusting your search query or filters',
            style: TextStyle(color: Colors.white38, fontSize: 14),
          ),
        ],
      ),
    );
  }
}
