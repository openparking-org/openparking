import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mapbox_maps_flutter/mapbox_maps_flutter.dart';
import 'package:flutter_dotenv/flutter_dotenv.dart';
import '../../core/providers/zone_provider.dart';
import '../../core/theme.dart';

class ZoneMapView extends ConsumerStatefulWidget {
  const ZoneMapView({super.key});

  @override
  ConsumerState<ZoneMapView> createState() => _ZoneMapViewState();
}

class _ZoneMapViewState extends ConsumerState<ZoneMapView> {
  MapboxMap? _mapboxMap;
  PointAnnotationManager? _pointAnnotationManager;
  final String _token = dotenv.env['MAPBOX_ACCESS_TOKEN'] ?? '';

  @override
  Widget build(BuildContext context) {
    final zoneState = ref.watch(zoneListProvider);
    if (_token.isNotEmpty) {
      MapboxOptions.setAccessToken(_token);
    }

    return Scaffold(
      body: _token.isEmpty
          ? const Center(child: Text('Mapbox Token is missing in .env'))
          : MapWidget(
              key: const ValueKey("mapWidget"),
              onMapCreated: _onMapCreated,
            ),
    );
  }

  Future<void> _onMapCreated(MapboxMap mapboxMap) async {
    _mapboxMap = mapboxMap;
    await mapboxMap.setCamera(CameraOptions(
      center: Point(coordinates: Position(103.851959, 1.290270)),
      zoom: 12.0,
    ));
    _pointAnnotationManager =
        await mapboxMap.annotations.createPointAnnotationManager();
    _updateAnnotations();
  }

  void _updateAnnotations() {
    if (_pointAnnotationManager == null) return;

    final zones = ref.read(zoneListProvider).zones;
    _pointAnnotationManager?.deleteAll();

    final List<PointAnnotationOptions> options = [];
    for (final zone in zones) {
      if (zone.latitude != 0.0 && zone.longitude != 0.0) {
        options.add(PointAnnotationOptions(
          geometry: Point(coordinates: Position(zone.longitude, zone.latitude)),
          textField: zone.name,
          textColor: AppTheme.primary.toARGB32(),
          textOffset: [0, 1.5],
        ));
      }
    }

    if (options.isNotEmpty) {
      _pointAnnotationManager?.createMulti(options);
    }
  }
}
