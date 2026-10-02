import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import '../../core/providers/permit_provider.dart';
import '../../core/auth_provider.dart';

class PermitUploadScreen extends ConsumerStatefulWidget {
  const PermitUploadScreen({super.key});

  @override
  ConsumerState<PermitUploadScreen> createState() => _PermitUploadScreenState();
}

class _PermitUploadScreenState extends ConsumerState<PermitUploadScreen> {
  final _formKey = GlobalKey<FormState>();
  final _permitNumberController = TextEditingController();
  final _authorityController = TextEditingController();

  DateTime _expiryDate = DateTime.now().add(const Duration(days: 365));
  File? _selectedImageFile;
  String? _base64Image;

  @override
  void dispose() {
    _permitNumberController.dispose();
    _authorityController.dispose();
    super.dispose();
  }

  Future<void> _pickImage(ImageSource source) async {
    final picker = ImagePicker();
    final picked = await picker.pickImage(source: source, imageQuality: 70);

    if (picked != null) {
      final bytes = await picked.readAsBytes();
      setState(() {
        _selectedImageFile = File(picked.path);
        _base64Image = base64Encode(bytes);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final permitState = ref.watch(permitProvider);

    final permit = permitState.permit;

    return Scaffold(
      backgroundColor: const Color(0xFF0B0F19),
      appBar: AppBar(
        title: const Text('Disability Permit Verification',
            style: TextStyle(fontWeight: FontWeight.bold)),
        backgroundColor: const Color(0xFF111827),
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () =>
                ref.read(permitProvider.notifier).fetchPermitStatus(),
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Header Info Card
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: const Color(0xFF1E293B),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: const Color(0xFF334155)),
              ),
              child: const Row(
                children: [
                  Icon(Icons.accessible, color: Color(0xFF818CF8), size: 36),
                  SizedBox(width: 14),
                  Expanded(
                    child: Text(
                      'Upload your valid accessible parking permit for AI Validator verification and a 15% discount.',
                      style: TextStyle(
                          color: Colors.white70, fontSize: 13, height: 1.4),
                    ),
                  )
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Active Permit Status Banner (if already submitted)
            if (permit != null) ...[
              _buildPermitStatusBanner(permit),
              const SizedBox(height: 24),
            ],

            // Submission Form
            const Text(
              'Submit / Update Permit Document',
              style: TextStyle(
                  color: Colors.white,
                  fontSize: 18,
                  fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 16),

            Form(
              key: _formKey,
              child: Column(
                children: [
                  TextFormField(
                    controller: _permitNumberController,
                    style: const TextStyle(color: Colors.white),
                    decoration: InputDecoration(
                      labelText: 'Permit Number',
                      labelStyle: const TextStyle(color: Colors.white60),
                      prefixIcon: const Icon(Icons.badge_outlined,
                          color: Color(0xFF6366F1)),
                      filled: true,
                      fillColor: const Color(0xFF1E293B),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: const BorderSide(color: Color(0xFF334155)),
                      ),
                    ),
                    validator: (val) =>
                        val == null || val.trim().isEmpty ? 'Required' : null,
                  ),
                  const SizedBox(height: 16),

                  TextFormField(
                    controller: _authorityController,
                    style: const TextStyle(color: Colors.white),
                    decoration: InputDecoration(
                      labelText: 'Issuing Authority / Jurisdiction',
                      labelStyle: const TextStyle(color: Colors.white60),
                      prefixIcon: const Icon(Icons.account_balance,
                          color: Color(0xFF6366F1)),
                      filled: true,
                      fillColor: const Color(0xFF1E293B),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: const BorderSide(color: Color(0xFF334155)),
                      ),
                    ),
                    validator: (val) =>
                        val == null || val.trim().isEmpty ? 'Required' : null,
                  ),
                  const SizedBox(height: 16),

                  // Expiry Date Picker
                  GestureDetector(
                    onTap: () async {
                      final picked = await showDatePicker(
                        context: context,
                        initialDate: _expiryDate,
                        firstDate: DateTime.now(),
                        lastDate:
                            DateTime.now().add(const Duration(days: 365 * 5)),
                      );
                      if (picked != null) setState(() => _expiryDate = picked);
                    },
                    child: Container(
                      padding: const EdgeInsets.all(16),
                      decoration: BoxDecoration(
                        color: const Color(0xFF1E293B),
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: const Color(0xFF334155)),
                      ),
                      child: Row(
                        children: [
                          const Icon(Icons.calendar_today,
                              color: Color(0xFF6366F1), size: 20),
                          const SizedBox(width: 12),
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text('Expiry Date',
                                  style: TextStyle(
                                      color: Colors.white54, fontSize: 11)),
                              Text(
                                DateFormat('MMM d, yyyy').format(_expiryDate),
                                style: const TextStyle(
                                    color: Colors.white,
                                    fontWeight: FontWeight.bold),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 20),

                  // Image Upload Picker Box
                  GestureDetector(
                    onTap: () => _showImageSourceModal(context),
                    child: Container(
                      height: 150,
                      width: double.infinity,
                      decoration: BoxDecoration(
                        color: const Color(0xFF1E293B),
                        borderRadius: BorderRadius.circular(16),
                        border: Border.all(
                          color: const Color(0xFF6366F1).withValues(alpha: 0.5),
                          style: BorderStyle.solid,
                        ),
                      ),
                      child: _selectedImageFile != null
                          ? ClipRRect(
                              borderRadius: BorderRadius.circular(16),
                              child: Image.file(_selectedImageFile!,
                                  fit: BoxFit.cover),
                            )
                          : const Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Icon(Icons.add_a_photo_outlined,
                                    size: 40, color: Color(0xFF6366F1)),
                                SizedBox(height: 8),
                                Text(
                                  'Tap to take photo or choose document',
                                  style: TextStyle(
                                      color: Colors.white70, fontSize: 13),
                                ),
                              ],
                            ),
                    ),
                  ),
                  const SizedBox(height: 24),

                  // Submit Action Button
                  SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                      onPressed:
                          permitState.isSubmitting ? null : _submitPermit,
                      icon: const Icon(Icons.cloud_upload),
                      label: permitState.isSubmitting
                          ? const CircularProgressIndicator(color: Colors.white)
                          : const Text(
                              'Submit to AI Validator',
                              style: TextStyle(
                                  fontSize: 16, fontWeight: FontWeight.bold),
                            ),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFF6366F1),
                        foregroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(vertical: 16),
                        shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12)),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildPermitStatusBanner(dynamic permit) {
    Color statusColor;
    IconData icon;
    switch (permit.status.toLowerCase()) {
      case 'approved':
        statusColor = const Color(0xFF10B981);
        icon = Icons.check_circle;
        break;
      case 'rejected':
        statusColor = Colors.redAccent;
        icon = Icons.cancel;
        break;
      default:
        statusColor = Colors.orangeAccent;
        icon = Icons.hourglass_top;
    }

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: statusColor.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: statusColor.withValues(alpha: 0.5)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, color: statusColor, size: 24),
              const SizedBox(width: 10),
              Text(
                'Status: ${permit.status.toUpperCase()}',
                style: TextStyle(
                    color: statusColor,
                    fontWeight: FontWeight.bold,
                    fontSize: 16),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            'Permit #${permit.permitNumber} - ${permit.issuingAuthority}',
            style: const TextStyle(
                color: Colors.white, fontWeight: FontWeight.bold),
          ),
          Text(
            'Expires: ${DateFormat("MMM d, yyyy").format(permit.expiryDate)}',
            style: const TextStyle(color: Colors.white70, fontSize: 12),
          ),
          if (permit.rejectionNotes != null &&
              permit.rejectionNotes!.isNotEmpty) ...[
            const SizedBox(height: 6),
            Text(
              'Reason: ${permit.rejectionNotes}',
              style: const TextStyle(color: Colors.redAccent, fontSize: 12),
            ),
          ],
        ],
      ),
    );
  }

  void _showImageSourceModal(BuildContext context) {
    showModalBottomSheet(
      context: context,
      backgroundColor: const Color(0xFF1E293B),
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) {
        return SafeArea(
          child: Wrap(
            children: [
              ListTile(
                leading: const Icon(Icons.camera_alt, color: Color(0xFF6366F1)),
                title: const Text('Take Photo (Camera)',
                    style: TextStyle(color: Colors.white)),
                onTap: () {
                  Navigator.pop(context);
                  _pickImage(ImageSource.camera);
                },
              ),
              ListTile(
                leading:
                    const Icon(Icons.photo_library, color: Color(0xFF6366F1)),
                title: const Text('Choose from Gallery',
                    style: TextStyle(color: Colors.white)),
                onTap: () {
                  Navigator.pop(context);
                  _pickImage(ImageSource.gallery);
                },
              ),
            ],
          ),
        );
      },
    );
  }

  Future<void> _submitPermit() async {
    if (!_formKey.currentState!.validate()) return;

    final userId = ref.read(authProvider).token ?? '';
    final success = await ref.read(permitProvider.notifier).submitPermit(
          userId: userId,
          permitNumber: _permitNumberController.text.trim(),
          issuingAuthority: _authorityController.text.trim(),
          expiryDate: _expiryDate,
          documentBase64: _base64Image,
        );

    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            success
                ? 'Permit submitted for AI verification!'
                : 'Permit submission failed.',
          ),
          backgroundColor: success ? const Color(0xFF10B981) : Colors.redAccent,
        ),
      );
    }
  }
}
