import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:cached_network_image/cached_network_image.dart';
import '../../models/zone.dart';
import '../../models/slot.dart';
import '../../models/floor_plan.dart';
import '../../models/route.dart';
import '../../services/zone_service.dart';
import '../../services/floor_plan_service.dart';
import '../booking/create_booking_screen.dart';
import '../../core/widgets/app_widgets.dart';

class IndoorMapScreen extends StatefulWidget {
  final String zoneId;
  final String? zoneName, targetSlotId, bookingId, recommendedSlotType;
  const IndoorMapScreen(
      {super.key,
      required this.zoneId,
      this.zoneName,
      this.targetSlotId,
      this.recommendedSlotType,
      this.bookingId});
  @override
  State<IndoorMapScreen> createState() => _IndoorMapScreenState();
}

class _IndoorMapScreenState extends State<IndoorMapScreen> {
  ZoneModel? zone;
  FloorPlanModel? selected;
  NavigationRouteModel? route;
  String? error, routeNotice;
  bool loading = true;
  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      loading = true;
      error = null;
      routeNotice = null;
      route = null;
    });
    try {
      final result = await ZoneService().getZoneDetail(widget.zoneId);
      if (!mounted) return;
      zone = result;
      selected = result?.floorPlans.isNotEmpty == true
          ? result!.floorPlans.first
          : null;
      if (widget.targetSlotId != null) {
        try {
          route = await FloorPlanService().getNavigationRoute(
              zoneId: widget.zoneId,
              targetSlotId: widget.targetSlotId!,
              bookingId: widget.bookingId);
          if (route != null && result != null) {
            for (final plan in result.floorPlans) {
              if (plan.id == route!.floorPlanId) selected = plan;
            }
          }
        } catch (e) {
          routeNotice = e.toString().replaceFirst('Exception: ', '');
        }
      }
      if (mounted) setState(() => loading = false);
    } catch (e) {
      if (mounted) {
        setState(() {
          loading = false;
          error = '$e';
        });
      }
    }
  }

  void _reserve(SlotModel slot) => Navigator.push(
      context,
      MaterialPageRoute<void>(
          builder: (_) => CreateBookingScreen(
              zoneId: widget.zoneId, preselectedSlotId: slot.id)));
  @override
  Widget build(BuildContext context) {
    final plan = selected;
    final slots = (zone?.slots ?? <SlotModel>[])
        .where((s) =>
            widget.recommendedSlotType == null ||
            s.type == widget.recommendedSlotType)
        .toList();
    final mapped = slots
        .where((slot) =>
            slot.floorPlanId == plan?.id &&
            slot.canvasX != null &&
            slot.canvasY != null)
        .toList();
    final width =
        plan != null && plan.imageWidthPx > 0 ? plan.imageWidthPx : 1000.0;
    final height =
        plan != null && plan.imageHeightPx > 0 ? plan.imageHeightPx : 600.0;
    return Scaffold(
      appBar: AppBar(
          title: Text(widget.zoneName ?? zone?.name ?? 'Parking spaces'),
          actions: [
            IconButton(
                tooltip: 'Refresh spaces',
                icon: const Icon(Icons.refresh),
                onPressed: _load)
          ]),
      body: loading
          ? const Center(child: CircularProgressIndicator())
          : error != null
              ? Center(
                  child: Column(mainAxisSize: MainAxisSize.min, children: [
                  Text(error!),
                  TextButton(onPressed: _load, child: const Text('Retry'))
                ]))
              : ListView(padding: pagePadding(context), children: [
                  if (routeNotice != null)
                    Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: Text(routeNotice!)),
                  if (plan != null) ...[
                    DropdownButton<FloorPlanModel>(
                        value: plan,
                        isExpanded: true,
                        items: zone!.floorPlans
                            .map((p) => DropdownMenuItem(
                                value: p, child: Text(p.floorName)))
                            .toList(),
                        onChanged: (p) => setState(() => selected = p)),
                    SizedBox(
                        height: 360,
                        child: ClipRect(
                            child: InteractiveViewer(
                          minScale: .5,
                          maxScale: 4,
                          constrained: false,
                          child: SizedBox(
                              width: width,
                              height: height,
                              child: Stack(children: [
                                if (plan.imageUrl.startsWith('data:image/'))
                                  Image.memory(
                                      base64Decode(
                                          plan.imageUrl.split(',').last),
                                      width: width,
                                      height: height,
                                      fit: BoxFit.fill)
                                else if (plan.imageUrl.isNotEmpty)
                                  CachedNetworkImage(
                                      imageUrl: plan.imageUrl,
                                      width: width,
                                      height: height,
                                      fit: BoxFit.fill,
                                      errorWidget: (_, __, ___) => const Center(
                                          child: Text(
                                              'Floor-plan image could not load.'))),
                                if (route?.floorPlanId == plan.id)
                                  CustomPaint(
                                      size: Size(width, height),
                                      painter: RoutePainter(route: route!)),
                                for (final slot in mapped)
                                  Positioned(
                                      left: slot.canvasX!,
                                      top: slot.canvasY!,
                                      width: slot.canvasWidth ?? 80,
                                      height: slot.canvasHeight ?? 40,
                                      child: GestureDetector(
                                          onTap: slot.isAvailable
                                              ? () => _reserve(slot)
                                              : null,
                                          child: Container(
                                            alignment: Alignment.center,
                                            decoration: BoxDecoration(
                                                color: slot.isAvailable
                                                    ? Colors.green
                                                        .withValues(alpha: .65)
                                                    : Colors.orange
                                                        .withValues(alpha: .65),
                                                border: Border.all(
                                                    color: slot.id ==
                                                            widget.targetSlotId
                                                        ? Colors.blue
                                                        : Colors.white,
                                                    width: slot.id ==
                                                            widget.targetSlotId
                                                        ? 3
                                                        : 1)),
                                            child: Text(slot.slotNumber,
                                                style: const TextStyle(
                                                    color: Colors.white,
                                                    fontWeight:
                                                        FontWeight.bold)),
                                          ))),
                              ])),
                        ))),
                    const Text(
                        'Pinch to zoom and drag the map. Green spaces are available.'),
                  ] else
                    const Text(
                        'A floor plan has not been published. Choose a space below; the attendant can help with directions.'),
                  if (route != null)
                    ...route!.instructions
                        .map((instruction) => Text(instruction)),
                  const SizedBox(height: 16),
                  Text('Parking spaces',
                      style: Theme.of(context).textTheme.titleMedium),
                  for (final slot in slots)
                    Card(
                        child: ListTile(
                      leading: Icon(slot.type == 'Accessible'
                          ? Icons.accessible
                          : slot.type == 'EV'
                              ? Icons.ev_station
                              : Icons.local_parking),
                      title: Text(
                          'Space ${slot.slotNumber}${slot.id == widget.targetSlotId ? ' · Your reservation' : ''}'),
                      subtitle: Text(
                          '${slot.type} · Floor ${slot.floor} · ${slot.status}'),
                      trailing: slot.isAvailable
                          ? TextButton(
                              onPressed: () => _reserve(slot),
                              child: const Text('Reserve'))
                          : null,
                    )),
                  if (slots.isEmpty)
                    const Text(
                        'No matching spaces are configured in this zone.'),
                ]),
    );
  }
}

class RoutePainter extends CustomPainter {
  final NavigationRouteModel route;
  RoutePainter({required this.route});
  @override
  void paint(Canvas canvas, Size size) {
    if (route.points.length < 2) return;
    final path = Path()..moveTo(route.points.first.x, route.points.first.y);
    for (final point in route.points.skip(1)) {
      path.lineTo(point.x, point.y);
    }
    canvas.drawPath(
        path,
        Paint()
          ..color = Colors.blue
          ..strokeWidth = 5
          ..style = PaintingStyle.stroke);
  }

  @override
  bool shouldRepaint(covariant RoutePainter oldDelegate) =>
      oldDelegate.route != route;
}
