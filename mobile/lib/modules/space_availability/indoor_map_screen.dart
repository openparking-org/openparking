import 'package:flutter/material.dart';

class IndoorMapScreen extends StatelessWidget {
  final String zoneId;
  final String? bookedSlotId;

  const IndoorMapScreen({
    super.key,
    required this.zoneId,
    this.bookedSlotId,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Indoor Parking Blueprint'),
        backgroundColor: const Color(0xFF111827),
      ),
      backgroundColor: const Color(0xFF0B0F19),
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              width: 320,
              height: 240,
              decoration: BoxDecoration(
                color: const Color(0xFF1E293B),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: const Color(0xFF334155)),
              ),
              child: Stack(
                alignment: Alignment.center,
                children: [
                  const Text(
                    'Indoor Floor Blueprint Map\n(A* Pathfinding Overlay)',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: Colors.white70),
                  ),
                  Positioned(
                    bottom: 20,
                    right: 20,
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: const Color(0xFF10B981),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: const Text('Slot A-102', style: TextStyle(color: Colors.white, fontSize: 12)),
                    ),
                  )
                ],
              ),
            ),
            const SizedBox(height: 24),
            Text(
              'Navigating to Slot: ${bookedSlotId ?? "A-102"}',
              style: const TextStyle(color: Colors.white, fontSize: 16, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            const Text(
              'Follow green path arrows on the blueprint.',
              style: TextStyle(color: Colors.grey, fontSize: 14),
            ),
          ],
        ),
      ),
    );
  }
}
