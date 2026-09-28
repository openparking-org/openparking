import 'package:flutter/material.dart';

class PermitUploadScreen extends StatefulWidget {
  const PermitUploadScreen({super.key});

  @override
  State<PermitUploadScreen> createState() => _PermitUploadScreenState();
}

class _PermitUploadScreenState extends State<PermitUploadScreen> {
  final _permitNumberController = TextEditingController();
  final _jurisdictionController = TextEditingController();
  bool _isSubmitting = false;

  void _submitPermit() {
    setState(() => _isSubmitting = true);
    Future.delayed(const Duration(milliseconds: 600), () {
      if (mounted) {
        setState(() => _isSubmitting = false);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content:
                Text('Permit submitted for AI verification (Validator Agent).'),
            backgroundColor: Color(0xFF10B981),
          ),
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Disability Permit Verification'),
        backgroundColor: const Color(0xFF111827),
      ),
      backgroundColor: const Color(0xFF0B0F19),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: const Color(0xFF1E293B),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: const Color(0xFF334155)),
              ),
              child: const Row(
                children: [
                  Icon(Icons.accessible, color: Color(0xFF818CF8), size: 32),
                  SizedBox(width: 14),
                  Expanded(
                    child: Text(
                      'Upload your valid accessible parking permit for automated verification.',
                      style: TextStyle(color: Colors.white70, fontSize: 13),
                    ),
                  )
                ],
              ),
            ),
            const SizedBox(height: 20),
            TextField(
              controller: _permitNumberController,
              style: const TextStyle(color: Colors.white),
              decoration: const InputDecoration(
                labelText: 'Permit Number',
                labelStyle: TextStyle(color: Colors.grey),
                enabledBorder: OutlineInputBorder(
                    borderSide: BorderSide(color: Color(0xFF334155))),
                focusedBorder: OutlineInputBorder(
                    borderSide: BorderSide(color: Color(0xFF6366F1))),
              ),
            ),
            const SizedBox(height: 16),
            TextField(
              controller: _jurisdictionController,
              style: const TextStyle(color: Colors.white),
              decoration: const InputDecoration(
                labelText: 'Issuing Authority / Jurisdiction',
                labelStyle: TextStyle(color: Colors.grey),
                enabledBorder: OutlineInputBorder(
                    borderSide: BorderSide(color: Color(0xFF334155))),
                focusedBorder: OutlineInputBorder(
                    borderSide: BorderSide(color: Color(0xFF6366F1))),
              ),
            ),
            const SizedBox(height: 24),
            ElevatedButton.icon(
              onPressed: _isSubmitting ? null : _submitPermit,
              icon: const Icon(Icons.cloud_upload),
              label: Text(
                  _isSubmitting ? 'Validating...' : 'Submit to AI Validator'),
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF6366F1),
                padding: const EdgeInsets.symmetric(vertical: 14),
              ),
            )
          ],
        ),
      ),
    );
  }
}
