import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:cached_network_image/cached_network_image.dart';
import '../../core/providers/floor_plan_provider.dart';
import '../../core/providers/zone_provider.dart';
import '../../models/slot.dart';
import '../../models/route.dart';
import '../booking/create_booking_screen.dart';

class IndoorMapScreen extends ConsumerStatefulWidget {
  final String zoneId;
  final String? zoneName;
  final String? targetSlotId;
  final String? bookingId;

  const IndoorMapScreen({
    super.key,
    required this.zoneId,
    this.zoneName,
    this.targetSlotId,
    this.bookingId,
  });

  @override
  ConsumerState<IndoorMapScreen> createState() => _IndoorMapScreenState();
}

class _IndoorMapScreenState extends ConsumerState<IndoorMapScreen> {
  final TransformationController _transformationController =
      TransformationController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(floorPlanProvider.notifier).loadFloorPlansForZone(widget.zoneId);
      ref.read(zoneListProvider.notifier).loadZoneDetail(widget.zoneId);

      if (widget.targetSlotId != null) {
        ref.read(floorPlanProvider.notifier).loadNavigationRoute(
              zoneId: widget.zoneId,
              targetSlotId: widget.targetSlotId!,
              bookingId: widget.bookingId,
            );
      }
    });
  }

  @override
  void dispose() {
    _transformationController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final fpState = ref.watch(floorPlanProvider);
    final zoneState = ref.watch(zoneListProvider);
    final zone = zoneState.selectedZone;

    final currentFloorPlan = fpState.selectedFloorPlan;

    return Scaffold(
      backgroundColor: const Color(0xFF0B0F19),
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              widget.zoneName ?? zone?.name ?? 'Zone Indoor Blueprint',
              style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
            ),
            if (currentFloorPlan != null)
              Text(
                'Floor: ${currentFloorPlan.floorName}',
                style: const TextStyle(fontSize: 12, color: Colors.white60),
              ),
          ],
        ),
        backgroundColor: const Color(0xFF111827),
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.center_focus_strong),
            onPressed: () {
              _transformationController.value = Matrix4.identity();
            },
            tooltip: 'Reset Zoom',
          ),
        ],
      ),
      body: fpState.isLoading
          ? const Center(
              child: CircularProgressIndicator(color: Color(0xFF6366F1)))
          : Column(
              children: [
                // Floor Selector Tabs
                if (fpState.floorPlans.isNotEmpty)
                  Container(
                    height: 50,
                    color: const Color(0xFF111827),
                    child: ListView.builder(
                      scrollDirection: Axis.horizontal,
                      padding: const EdgeInsets.symmetric(
                          horizontal: 12, vertical: 8),
                      itemCount: fpState.floorPlans.length,
                      itemBuilder: (context, index) {
                        final plan = fpState.floorPlans[index];
                        final isSelected = plan.id == currentFloorPlan?.id;
                        return Padding(
                          padding: const EdgeInsets.only(right: 8),
                          child: ChoiceChip(
                            label: Text(plan.floorName),
                            selected: isSelected,
                            selectedColor: const Color(0xFF6366F1),
                            backgroundColor: const Color(0xFF1E293B),
                            labelStyle: TextStyle(
                              color: isSelected ? Colors.white : Colors.white70,
                              fontWeight: isSelected
                                  ? FontWeight.bold
                                  : FontWeight.normal,
                            ),
                            onSelected: (_) {
                              ref
                                  .read(floorPlanProvider.notifier)
                                  .selectFloorPlan(plan);
                            },
                          ),
                        );
                      },
                    ),
                  ),

                // Interactive Blueprint Viewer
                Expanded(
                  child: Stack(
                    children: [
                      InteractiveViewer(
                        transformationController: _transformationController,
                        minScale: 0.5,
                        maxScale: 4.0,
                        boundaryMargin: const EdgeInsets.all(200),
                        child: Center(
                          child: Container(
                            width: 600,
                            height: 600,
                            decoration: BoxDecoration(
                              color: const Color(0xFF1E293B),
                              borderRadius: BorderRadius.circular(16),
                              border: Border.all(
                                  color: const Color(0xFF334155), width: 2),
                            ),
                            child: Stack(
                              children: [
                                // Background Image or Grid Fallback
                                if (currentFloorPlan?.imageUrl != null &&
                                    currentFloorPlan!.imageUrl.isNotEmpty)
                                  CachedNetworkImage(
                                    imageUrl: currentFloorPlan.imageUrl,
                                    width: 600,
                                    height: 600,
                                    fit: BoxFit.cover,
                                    errorWidget: (_, __, ___) =>
                                        _buildGridPattern(),
                                  )
                                else
                                  _buildGridPattern(),

                                // Custom Route Painter (A* glowing path)
                                if (fpState.navigationRoute != null)
                                  CustomPaint(
                                    size: const Size(600, 600),
                                    painter: RoutePainter(
                                        route: fpState.navigationRoute!),
                                  ),

                                // Slot Overlays
                                if (zone != null && zone.slots.isNotEmpty)
                                  ..._buildSlotOverlays(context, zone.slots),
                              ],
                            ),
                          ),
                        ),
                      ),

                      // Legend Overlay Card
                      Positioned(
                        left: 16,
                        bottom: 16,
                        child: Container(
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(
                            color:
                                const Color(0xFF111827).withValues(alpha: 0.9),
                            borderRadius: BorderRadius.circular(12),
                            border: Border.all(color: const Color(0xFF334155)),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              _buildLegendItem(Colors.greenAccent, 'Available'),
                              const SizedBox(height: 4),
                              _buildLegendItem(Colors.redAccent, 'Occupied'),
                              const SizedBox(height: 4),
                              _buildLegendItem(Colors.blueAccent, 'Disability'),
                              const SizedBox(height: 4),
                              _buildLegendItem(Colors.orangeAccent, 'Reserved'),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
    );
  }

  Widget _buildGridPattern() {
    return CustomPaint(
      size: const Size(600, 600),
      painter: GridPainter(),
    );
  }

  List<Widget> _buildSlotOverlays(BuildContext context, List<SlotModel> slots) {
    // Generate layout positions for slots dynamically if boundingBoxJson is not provided
    final widgets = <Widget>[];

    for (int i = 0; i < slots.length; i++) {
      final slot = slots[i];
      final col = i % 5;
      final row = i ~/ 5;

      final double left = 60.0 + col * 100.0;
      final double top = 60.0 + row * 110.0;

      final isTarget = slot.id == widget.targetSlotId;

      Color color;
      if (slot.type.toLowerCase() == 'disability') {
        color = Colors.blueAccent;
      } else {
        switch (slot.status.toLowerCase()) {
          case 'available':
            color = const Color(0xFF10B981);
            break;
          case 'occupied':
            color = Colors.redAccent;
            break;
          case 'reserved':
            color = Colors.orangeAccent;
            break;
          default:
            color = Colors.grey;
        }
      }

      widgets.add(
        Positioned(
          left: left,
          top: top,
          child: GestureDetector(
            onTap: () => _showSlotDetailsBottomSheet(context, slot),
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 300),
              width: 80,
              height: 90,
              decoration: BoxDecoration(
                color: color.withValues(alpha: isTarget ? 0.4 : 0.25),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(
                  color: isTarget ? Colors.yellowAccent : color,
                  width: isTarget ? 3 : 1.5,
                ),
                boxShadow: isTarget
                    ? [
                        BoxShadow(
                          color: Colors.yellowAccent.withValues(alpha: 0.6),
                          blurRadius: 12,
                          spreadRadius: 2,
                        )
                      ]
                    : [],
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    slot.type.toLowerCase() == 'disability'
                        ? Icons.accessible
                        : Icons.directions_car,
                    color: isTarget ? Colors.yellowAccent : color,
                    size: 24,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    slot.slotNumber,
                    style: TextStyle(
                      color: isTarget ? Colors.yellowAccent : Colors.white,
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                    ),
                  ),
                  Text(
                    slot.status,
                    style: TextStyle(color: color, fontSize: 10),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    return widgets;
  }

  Widget _buildLegendItem(Color color, String label) {
    return Row(
      children: [
        Container(
          width: 10,
          height: 10,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: 8),
        Text(
          label,
          style: const TextStyle(color: Colors.white70, fontSize: 11),
        ),
      ],
    );
  }

  void _showSlotDetailsBottomSheet(BuildContext context, SlotModel slot) {
    showModalBottomSheet(
      context: context,
      backgroundColor: const Color(0xFF1E293B),
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) {
        return Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    'Slot ${slot.slotNumber}',
                    style: const TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.bold,
                      color: Colors.white,
                    ),
                  ),
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: slot.isAvailable
                          ? const Color(0xFF10B981).withValues(alpha: 0.2)
                          : Colors.redAccent.withValues(alpha: 0.2),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Text(
                      slot.status,
                      style: TextStyle(
                        color: slot.isAvailable
                            ? const Color(0xFF10B981)
                            : Colors.redAccent,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Text('Type: ${slot.type}',
                  style: const TextStyle(color: Colors.white70)),
              Text('Floor Level: ${slot.floor}',
                  style: const TextStyle(color: Colors.white70)),
              const SizedBox(height: 24),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: slot.isAvailable
                      ? () {
                          Navigator.pop(context);
                          Navigator.push(
                            context,
                            MaterialPageRoute(
                              builder: (context) => CreateBookingScreen(
                                zoneId: widget.zoneId,
                                preselectedSlotId: slot.id,
                              ),
                            ),
                          );
                        }
                      : null,
                  icon: const Icon(Icons.bookmark_add),
                  label: const Text('Reserve This Slot'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF6366F1),
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12)),
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class GridPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = const Color(0xFF334155).withValues(alpha: 0.3)
      ..strokeWidth = 1;

    const double step = 30;
    for (double x = 0; x < size.width; x += step) {
      canvas.drawLine(Offset(x, 0), Offset(x, size.height), paint);
    }
    for (double y = 0; y < size.height; y += step) {
      canvas.drawLine(Offset(0, y), Offset(size.width, y), paint);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

class RoutePainter extends CustomPainter {
  final NavigationRouteModel route;

  RoutePainter({required this.route});

  @override
  void paint(Canvas canvas, Size size) {
    if (route.points.length < 2) return;

    final paint = Paint()
      ..color = Colors.cyanAccent
      ..strokeWidth = 4
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;

    final path = Path();
    path.moveTo(route.points.first.x, route.points.first.y);

    for (int i = 1; i < route.points.length; i++) {
      path.lineTo(route.points[i].x, route.points[i].y);
    }

    // Glow Effect
    final glowPaint = Paint()
      ..color = Colors.cyanAccent.withValues(alpha: 0.4)
      ..strokeWidth = 8
      ..style = PaintingStyle.stroke
      ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 6);

    canvas.drawPath(path, glowPaint);
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant RoutePainter oldDelegate) =>
      oldDelegate.route != route;
}
