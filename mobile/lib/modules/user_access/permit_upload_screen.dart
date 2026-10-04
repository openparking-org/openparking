import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import '../../core/providers/permit_provider.dart';
import '../../core/auth_provider.dart';
import '../../core/theme.dart';

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
    try {
      final picked = await ImagePicker()
          .pickImage(source: source, imageQuality: 70, maxWidth: 2000);
      if (picked == null) return;
      final bytes = await picked.readAsBytes();
      if (!mounted) return;
      if (bytes.length > 5 * 1024 * 1024)
        throw Exception('Choose an image smaller than 5 MB.');
      setState(() {
        _selectedImageFile = File(picked.path);
        _base64Image = base64Encode(bytes);
      });
    } catch (e) {
      if (mounted)
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Could not attach image: $e')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final permitState = ref.watch(permitProvider);
    final permit = permitState.permit;

    return Scaffold(
      backgroundColor: AppTheme.surface,
      body: CustomScrollView(
        slivers: [
          SliverAppBar(
            floating: true,
            snap: true,
            backgroundColor: AppTheme.surfacePure,
            surfaceTintColor: Colors.transparent,
            title: Text(
              'Disability Permit Verification',
              style: AppTheme.titleMd.copyWith(fontWeight: FontWeight.w700),
            ),
            actions: [
              IconButton(
                icon: const Icon(Icons.refresh, size: 20),
                color: AppTheme.primary,
                onPressed: () =>
                    ref.read(permitProvider.notifier).fetchPermitStatus(),
              ),
            ],
          ),
          SliverPadding(
            padding: const EdgeInsets.all(AppTheme.margin),
            sliver: SliverList(
              delegate: SliverChildListDelegate([
                // Header Info Card
                Container(
                  padding: const EdgeInsets.all(AppTheme.spaceMd),
                  decoration: BoxDecoration(
                    color: AppTheme.surfacePure,
                    borderRadius: BorderRadius.circular(AppTheme.radiusXl),
                    border: Border.all(color: AppTheme.borderSubtle),
                  ),
                  child: Row(
                    children: [
                      Container(
                        width: 44,
                        height: 44,
                        decoration: BoxDecoration(
                          color: AppTheme.primary.withValues(alpha: 0.06),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(Icons.accessible,
                            color: AppTheme.primary, size: 24),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: Text(
                          'Upload your valid accessible parking permit for AI Validator verification and a 15% discount.',
                          style: AppTheme.bodySm.copyWith(
                            color: AppTheme.textSecondary,
                            height: 1.4,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppTheme.spaceMd),

                // Active Permit Status Banner (if already submitted)
                if (permit != null) ...[
                  _buildPermitStatusBanner(permit),
                  const SizedBox(height: AppTheme.spaceLg),
                ],

                // Submission Form
                Text(
                  'Submit / Update Permit Document',
                  style: AppTheme.titleMd.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: AppTheme.spaceMd),

                Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Permit Number', style: AppTheme.labelMd),
                      const SizedBox(height: 6),
                      TextFormField(
                        controller: _permitNumberController,
                        style: AppTheme.bodyMd,
                        decoration: const InputDecoration(
                          hintText: 'Enter permit number',
                          prefixIcon: Icon(Icons.badge_outlined,
                              color: AppTheme.textTertiary, size: 18),
                        ),
                        validator: (val) => val == null || val.trim().isEmpty
                            ? 'Required'
                            : null,
                      ),
                      const SizedBox(height: AppTheme.spaceMd),

                      const Text('Issuing Authority / Jurisdiction',
                          style: AppTheme.labelMd),
                      const SizedBox(height: 6),
                      TextFormField(
                        controller: _authorityController,
                        style: AppTheme.bodyMd,
                        decoration: const InputDecoration(
                          hintText: 'Enter issuing authority',
                          prefixIcon: Icon(Icons.account_balance,
                              color: AppTheme.textTertiary, size: 18),
                        ),
                        validator: (val) => val == null || val.trim().isEmpty
                            ? 'Required'
                            : null,
                      ),
                      const SizedBox(height: AppTheme.spaceMd),

                      // Expiry Date Picker
                      const Text('Expiry Date', style: AppTheme.labelMd),
                      const SizedBox(height: 6),
                      GestureDetector(
                        onTap: () async {
                          final picked = await showDatePicker(
                            context: context,
                            initialDate: _expiryDate,
                            firstDate: DateTime.now(),
                            lastDate: DateTime.now()
                                .add(const Duration(days: 365 * 5)),
                          );
                          if (picked != null) {
                            setState(() => _expiryDate = picked);
                          }
                        },
                        child: Container(
                          padding: const EdgeInsets.all(14),
                          decoration: BoxDecoration(
                            color: AppTheme.surfaceSubtle,
                            borderRadius:
                                BorderRadius.circular(AppTheme.radiusXl),
                          ),
                          child: Row(
                            children: [
                              const Icon(Icons.calendar_today,
                                  color: AppTheme.textTertiary, size: 18),
                              const SizedBox(width: 12),
                              Text(
                                DateFormat('MMM d, yyyy').format(_expiryDate),
                                style: AppTheme.bodyMd.copyWith(
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                              const Spacer(),
                              const Icon(Icons.chevron_right,
                                  color: AppTheme.textTertiary, size: 18),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: AppTheme.spaceMd),

                      // Image Upload Picker Box
                      const Text('Permit Document', style: AppTheme.labelMd),
                      const SizedBox(height: 6),
                      GestureDetector(
                        onTap: () => _showImageSourceModal(context),
                        child: Container(
                          height: 150,
                          width: double.infinity,
                          decoration: BoxDecoration(
                            color: AppTheme.surfaceSubtle,
                            borderRadius:
                                BorderRadius.circular(AppTheme.radiusXl),
                            border: Border.all(
                              color: AppTheme.borderSubtle,
                              style: BorderStyle.solid,
                            ),
                          ),
                          child: _selectedImageFile != null
                              ? ClipRRect(
                                  borderRadius:
                                      BorderRadius.circular(AppTheme.radiusXl),
                                  child: Image.file(_selectedImageFile!,
                                      fit: BoxFit.cover),
                                )
                              : Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    Container(
                                      width: 48,
                                      height: 48,
                                      decoration: BoxDecoration(
                                        color: AppTheme.surfacePure,
                                        shape: BoxShape.circle,
                                        border: Border.all(
                                            color: AppTheme.borderSubtle),
                                      ),
                                      child: const Icon(
                                          Icons.add_a_photo_outlined,
                                          size: 22,
                                          color: AppTheme.primary),
                                    ),
                                    const SizedBox(height: 8),
                                    Text(
                                      'Tap to take photo or choose document',
                                      style: AppTheme.bodySm.copyWith(
                                        color: AppTheme.textTertiary,
                                      ),
                                    ),
                                  ],
                                ),
                        ),
                      ),
                      const SizedBox(height: AppTheme.spaceLg),

                      // Submit Action Button
                      SizedBox(
                        width: double.infinity,
                        height: 48,
                        child: ElevatedButton.icon(
                          onPressed:
                              permitState.isSubmitting ? null : _submitPermit,
                          icon: const Icon(Icons.cloud_upload, size: 20),
                          label: permitState.isSubmitting
                              ? const SizedBox(
                                  width: 24,
                                  height: 24,
                                  child: CircularProgressIndicator(
                                    color: Colors.white,
                                    strokeWidth: 2.5,
                                  ),
                                )
                              : Text(
                                  'Submit to AI Validator',
                                  style: AppTheme.labelLg.copyWith(
                                    color: AppTheme.onPrimary,
                                  ),
                                ),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppTheme.primary,
                            foregroundColor: AppTheme.onPrimary,
                            elevation: 0,
                            shape: RoundedRectangleBorder(
                              borderRadius:
                                  BorderRadius.circular(AppTheme.radiusXl),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 80),
              ]),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPermitStatusBanner(dynamic permit) {
    Color statusColor;
    IconData icon;
    switch (permit.status.toLowerCase()) {
      case 'verified':
        statusColor = AppTheme.accentSuccess;
        icon = Icons.check_circle;
        break;
      case 'rejected':
        statusColor = AppTheme.accentCritical;
        icon = Icons.cancel;
        break;
      default:
        statusColor = const Color(0xFFF59E0B);
        icon = Icons.hourglass_top;
    }

    return Container(
      padding: const EdgeInsets.all(AppTheme.spaceMd),
      decoration: BoxDecoration(
        color: statusColor.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(AppTheme.radiusXl),
        border: Border.all(color: statusColor.withValues(alpha: 0.2)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 32,
                height: 32,
                decoration: BoxDecoration(
                  color: statusColor.withValues(alpha: 0.1),
                  shape: BoxShape.circle,
                ),
                child: Icon(icon, color: statusColor, size: 18),
              ),
              const SizedBox(width: 10),
              Text(
                'Status: ${permit.status.toUpperCase()}',
                style: AppTheme.labelLg.copyWith(
                  color: statusColor,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            'Permit #${permit.permitNumber} - ${permit.issuingAuthority}',
            style: AppTheme.labelLg.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 2),
          Text(
            'Expires: ${DateFormat("MMM d, yyyy").format(permit.expiryDate)}',
            style: AppTheme.bodySm.copyWith(color: AppTheme.textSecondary),
          ),
          if (permit.rejectionNotes != null &&
              permit.rejectionNotes!.isNotEmpty) ...[
            const SizedBox(height: 6),
            Text(
              'Reason: ${permit.rejectionNotes}',
              style: AppTheme.bodySm.copyWith(
                color: AppTheme.accentCritical,
              ),
            ),
          ],
        ],
      ),
    );
  }

  void _showImageSourceModal(BuildContext context) {
    showModalBottomSheet(
      context: context,
      backgroundColor: AppTheme.surfacePure,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) {
        return SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              // Handle bar
              Center(
                child: Container(
                  margin: const EdgeInsets.only(top: 12),
                  width: 40,
                  height: 4,
                  decoration: BoxDecoration(
                    color: AppTheme.surfaceMuted,
                    borderRadius: BorderRadius.circular(100),
                  ),
                ),
              ),
              ListTile(
                leading: Container(
                  width: 36,
                  height: 36,
                  decoration: BoxDecoration(
                    color: AppTheme.surfaceSubtle,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: const Icon(Icons.camera_alt,
                      color: AppTheme.primary, size: 18),
                ),
                title:
                    const Text('Take Photo (Camera)', style: AppTheme.labelLg),
                onTap: () {
                  Navigator.pop(context);
                  _pickImage(ImageSource.camera);
                },
              ),
              ListTile(
                leading: Container(
                  width: 36,
                  height: 36,
                  decoration: BoxDecoration(
                    color: AppTheme.surfaceSubtle,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: const Icon(Icons.photo_library,
                      color: AppTheme.primary, size: 18),
                ),
                title:
                    const Text('Choose from Gallery', style: AppTheme.labelLg),
                onTap: () {
                  Navigator.pop(context);
                  _pickImage(ImageSource.gallery);
                },
              ),
              const SizedBox(height: 8),
            ],
          ),
        );
      },
    );
  }

  Future<void> _submitPermit() async {
    if (!_formKey.currentState!.validate()) return;

    final userId = ref.read(authProvider).user?.id;
    if (userId == null || _base64Image == null) {
      ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Sign in and attach a permit image.')));
      return;
    }
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
                : ref.read(permitProvider).error ?? 'Permit submission failed.',
          ),
          backgroundColor:
              success ? AppTheme.accentSuccess : AppTheme.accentCritical,
        ),
      );
    }
  }
}
