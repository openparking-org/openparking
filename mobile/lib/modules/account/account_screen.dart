import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/auth_provider.dart';
import '../../core/theme.dart';
import '../enforcement/penalties_screen.dart';
import '../user_access/permit_upload_screen.dart';

class AccountScreen extends ConsumerWidget {
  const AccountScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      backgroundColor: AppTheme.surface,
      appBar: AppBar(
        title: Text('Account',
            style: AppTheme.titleMd.copyWith(fontWeight: FontWeight.w700)),
        backgroundColor: AppTheme.surfacePure,
        surfaceTintColor: Colors.transparent,
      ),
      body: ListView(
        padding: const EdgeInsets.all(AppTheme.margin),
        children: [
          // Profile Header
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
                  width: 56,
                  height: 56,
                  decoration: BoxDecoration(
                    color: AppTheme.primary.withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.person,
                      size: 28, color: AppTheme.primary),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        ref.watch(authProvider).user?.fullName ??
                            'Driver Account',
                        style: AppTheme.titleMd
                            .copyWith(fontWeight: FontWeight.w700),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        ref.watch(authProvider).user?.email ?? '',
                        style: AppTheme.bodySm
                            .copyWith(color: AppTheme.textSecondary),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),

          const SizedBox(height: AppTheme.spaceLg),

          Text('MANAGEMENT',
              style: AppTheme.labelSm.copyWith(color: AppTheme.textTertiary)),
          const SizedBox(height: AppTheme.spaceSm),

          _buildMenuTile(
            context,
            icon: Icons.shield_outlined,
            title: 'Penalties & Enforcement',
            subtitle: 'View and dispute parking fines',
            onTap: () {
              Navigator.push(context,
                  MaterialPageRoute(builder: (_) => const PenaltiesScreen()));
            },
          ),

          const SizedBox(height: AppTheme.spaceSm),

          _buildMenuTile(
            context,
            icon: Icons.accessible,
            title: 'Disability Permit',
            subtitle: 'Upload permit for verification',
            onTap: () {
              Navigator.push(
                  context,
                  MaterialPageRoute(
                      builder: (_) => const PermitUploadScreen()));
            },
          ),

          const SizedBox(height: AppTheme.spaceLg),

          // Logout Button
          SizedBox(
            width: double.infinity,
            height: 48,
            child: OutlinedButton.icon(
              onPressed: () {
                ref.read(authProvider.notifier).logout();
              },
              icon: const Icon(Icons.logout, size: 18),
              label: const Text('Sign Out'),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppTheme.accentCritical,
                side: const BorderSide(color: AppTheme.accentCritical),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(AppTheme.radiusXl),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildMenuTile(BuildContext context,
      {required IconData icon,
      required String title,
      required String subtitle,
      required VoidCallback onTap}) {
    return Container(
      decoration: BoxDecoration(
        color: AppTheme.surfacePure,
        borderRadius: BorderRadius.circular(AppTheme.radiusXl),
        border: Border.all(color: AppTheme.borderSubtle),
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: BorderRadius.circular(AppTheme.radiusXl),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
            child: Row(
              children: [
                Container(
                  width: 36,
                  height: 36,
                  decoration: BoxDecoration(
                    color: AppTheme.surfaceSubtle,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Icon(icon, color: AppTheme.primary, size: 18),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(title,
                          style: AppTheme.labelLg
                              .copyWith(fontWeight: FontWeight.w700)),
                      Text(subtitle,
                          style: AppTheme.bodySm
                              .copyWith(color: AppTheme.textSecondary)),
                    ],
                  ),
                ),
                const Icon(Icons.chevron_right,
                    color: AppTheme.textTertiary, size: 20),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
