import 'package:flutter/material.dart';
import '../../services/zone_service.dart';

class IndoorMapScreen extends StatefulWidget {
  final String zoneId;
  final String? bookedSlotId;

  const IndoorMapScreen({
    super.key,
    required this.zoneId,
    this.bookedSlotId,
  });

  @override
  State<IndoorMapScreen> createState() => _IndoorMapScreenState();
}

class _IndoorMapScreenState extends State<IndoorMapScreen> {
  final ZoneService _zoneService = ZoneService();
  List<ZoneModel> _zones = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _fetchZones();
  }

  Future<void> _fetchZones() async {
    final zones = await _zoneService.getZones();
    if (mounted) {
      setState(() {
        _zones = zones;
        _isLoading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Zone Discovery'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _fetchZones,
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : ListView.builder(
              itemCount: _zones.length,
              itemBuilder: (context, index) {
                final zone = _zones[index];
                return ListTile(
                  title: Text(zone.name),
                  subtitle: Text('Capacity: ${zone.capacity} | Available: ${zone.availableSlots}'),
                  trailing: Text('Multiplier: ${zone.currentPriceMultiplier}x'),
                );
              },
            ),
    );
  }
}
