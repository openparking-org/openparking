import 'package:flutter/material.dart';

class PenaltiesScreen extends StatelessWidget {
  const PenaltiesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Enforcement & Overstay Notice'),
        backgroundColor: const Color(0xFF111827),
      ),
      backgroundColor: const Color(0xFF0B0F19),
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: const Color(0x1FEF4444),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: const Color(0x66EF4444)),
              ),
              child: const Row(
                children: [
                  Icon(Icons.warning_amber_rounded,
                      color: Color(0xFFEF4444), size: 28),
                  SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      'AI Overstay Detection Active. Please vacate your space within the 15-minute grace period.',
                      style: TextStyle(color: Colors.white, fontSize: 13),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),
            const Text(
              'Past Enforcement Records',
              style: TextStyle(
                  color: Colors.white,
                  fontSize: 16,
                  fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: const Color(0xFF1E293B),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Row(
                mainAxisAlignment: MainAxisAlignment.spaceAround,
                children: [
                  Text('No outstanding penalties found.',
                      style: TextStyle(color: Colors.grey, fontSize: 13)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
