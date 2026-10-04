import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../core/providers/penalty_provider.dart';
import '../../core/theme.dart';
import '../../core/widgets/app_widgets.dart';
import '../../models/penalty.dart';

class PenaltiesScreen extends ConsumerWidget {
  const PenaltiesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final penaltyState = ref.watch(penaltyProvider);

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
              'Penalties & Enforcement',
              style: AppTheme.titleMd.copyWith(fontWeight: FontWeight.w700),
            ),
            actions: [
              IconButton(
                icon: const Icon(Icons.refresh, size: 20),
                color: AppTheme.primary,
                onPressed: () =>
                    ref.read(penaltyProvider.notifier).fetchPenalties(),
              ),
            ],
          ),
          SliverPadding(
            padding: pagePadding(context),
            sliver: SliverList(
              delegate: SliverChildListDelegate([
                // ── AI OVERSTAY BANNER ──
                Container(
                  padding: const EdgeInsets.all(AppTheme.spaceMd),
                  decoration: BoxDecoration(
                    color: AppTheme.accentCritical.withValues(alpha: 0.06),
                    borderRadius: BorderRadius.circular(AppTheme.radiusXl),
                    border: Border.all(
                      color: AppTheme.accentCritical.withValues(alpha: 0.2),
                    ),
                  ),
                  child: Row(
                    children: [
                      Container(
                        width: 40,
                        height: 40,
                        decoration: BoxDecoration(
                          color: AppTheme.accentCritical.withValues(alpha: 0.1),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(Icons.warning_amber_rounded,
                            color: AppTheme.accentCritical, size: 22),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: Text(
                          'AI Overstay Detection active across all zones. 15-minute grace period strictly enforced.',
                          style: AppTheme.bodySm.copyWith(
                            color: AppTheme.textPrimary,
                            height: 1.4,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppTheme.spaceLg),

                // ── SECTION HEADER ──
                Text(
                  'Issued Penalties',
                  style: AppTheme.titleMd.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: AppTheme.spaceSm),

                // ── PENALTY LIST ──
                if (penaltyState.isLoading)
                  const Padding(
                    padding: EdgeInsets.all(32),
                    child: Center(
                      child: CircularProgressIndicator(
                        color: AppTheme.primary,
                        strokeWidth: 2,
                      ),
                    ),
                  )
                else if (penaltyState.penalties.isEmpty)
                  _buildEmptyState()
                else
                  ...penaltyState.penalties.map(
                    (penalty) => Padding(
                      padding: const EdgeInsets.only(bottom: AppTheme.spaceSm),
                      child: _buildPenaltyCard(context, ref, penalty),
                    ),
                  ),

                const SizedBox(height: 80), // Bottom nav padding
              ]),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildEmptyState() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(32),
      decoration: BoxDecoration(
        color: AppTheme.surfacePure,
        borderRadius: BorderRadius.circular(AppTheme.radiusXl),
        border: Border.all(color: AppTheme.borderSubtle),
      ),
      child: Column(
        children: [
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: AppTheme.accentSuccess.withValues(alpha: 0.1),
              shape: BoxShape.circle,
            ),
            child: const Icon(Icons.verified_user_outlined,
                size: 28, color: AppTheme.accentSuccess),
          ),
          const SizedBox(height: 12),
          Text(
            'Clean Enforcement Record',
            style: AppTheme.labelLg.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 4),
          Text(
            'No penalties or active disputes found.',
            style: AppTheme.bodySm.copyWith(color: AppTheme.textTertiary),
          ),
        ],
      ),
    );
  }

  Widget _buildPenaltyCard(
      BuildContext context, WidgetRef ref, PenaltyModel penalty) {
    Color statusColor;
    switch (penalty.status.toLowerCase()) {
      case 'approved':
      case 'pending':
        statusColor = AppTheme.accentCritical;
        break;
      case 'disputed':
        statusColor = const Color(0xFFF59E0B);
        break;
      case 'waived':
      case 'paid':
        statusColor = AppTheme.accentSuccess;
        break;
      default:
        statusColor = AppTheme.textTertiary;
    }

    return Container(
      decoration: BoxDecoration(
        color: AppTheme.surfacePure,
        borderRadius: BorderRadius.circular(AppTheme.radiusXl),
        border: Border.all(color: AppTheme.borderSubtle),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppTheme.spaceMd),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    Container(
                      width: 32,
                      height: 32,
                      decoration: BoxDecoration(
                        color: AppTheme.surfaceSubtle,
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: const Icon(Icons.receipt_long,
                          color: AppTheme.primary, size: 18),
                    ),
                    const SizedBox(width: 10),
                    Text(
                      penalty.reason,
                      style: AppTheme.labelLg.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ],
                ),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: statusColor.withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(100),
                    border: Border.all(
                      color: statusColor.withValues(alpha: 0.3),
                    ),
                  ),
                  child: Text(
                    penalty.status,
                    style: AppTheme.labelSm.copyWith(
                      color: statusColor,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Issued On',
                      style: AppTheme.labelSm.copyWith(
                        color: AppTheme.textTertiary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      DateFormat('MMM d, yyyy - HH:mm')
                          .format(penalty.issuedAt),
                      style: AppTheme.bodySm.copyWith(
                        color: AppTheme.textSecondary,
                      ),
                    ),
                  ],
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(
                      'Fine Amount',
                      style: AppTheme.labelSm.copyWith(
                        color: AppTheme.textTertiary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '${penalty.currency} ${penalty.amount.toStringAsFixed(2)}',
                      style: AppTheme.titleMd.copyWith(
                        color: AppTheme.accentCritical,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ],
                ),
              ],
            ),
            if (penalty.disputeNotes != null &&
                penalty.disputeNotes!.isNotEmpty) ...[
              const SizedBox(height: 12),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: AppTheme.surfaceSubtle,
                  borderRadius: BorderRadius.circular(AppTheme.radiusLg),
                ),
                child: Text(
                  'Dispute Notes: ${penalty.disputeNotes}',
                  style: AppTheme.bodySm.copyWith(
                    color: AppTheme.textSecondary,
                  ),
                ),
              ),
            ],
            if (penalty.isDisputable) ...[
              const SizedBox(height: 14),
              SizedBox(
                width: double.infinity,
                height: 40,
                child: OutlinedButton.icon(
                  onPressed: () => _showDisputeDialog(context, ref, penalty),
                  icon: const Icon(Icons.gavel, size: 16),
                  label: Text(
                    'Dispute Penalty (7-Day Window)',
                    style: AppTheme.labelMd.copyWith(
                      color: const Color(0xFFF59E0B),
                    ),
                  ),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: const Color(0xFFF59E0B),
                    side: const BorderSide(color: Color(0xFFF59E0B)),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppTheme.radiusLg),
                    ),
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  void _showDisputeDialog(
      BuildContext context, WidgetRef ref, PenaltyModel penalty) {
    final controller = TextEditingController();

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppTheme.surfacePure,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) {
        return Padding(
          padding: EdgeInsets.only(
            left: 20,
            right: 20,
            top: 20,
            bottom: MediaQuery.of(context).viewInsets.bottom + 20,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Handle bar
              Center(
                child: Container(
                  width: 40,
                  height: 4,
                  decoration: BoxDecoration(
                    color: AppTheme.surfaceMuted,
                    borderRadius: BorderRadius.circular(100),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              const Text(
                'Dispute Penalty Fine',
                style: AppTheme.headlineSm,
              ),
              const SizedBox(height: 8),
              Text(
                'Please explain the reason for your dispute. Parking admins will review your request.',
                style: AppTheme.bodyMd.copyWith(color: AppTheme.textSecondary),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: controller,
                maxLines: 4,
                style: AppTheme.bodyMd,
                decoration: InputDecoration(
                  hintText:
                      'Enter dispute reason (e.g., machine malfunction, emergency)...',
                  hintStyle:
                      AppTheme.bodyMd.copyWith(color: AppTheme.textTertiary),
                  filled: true,
                  fillColor: AppTheme.surfaceSubtle,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppTheme.radiusXl),
                    borderSide: BorderSide.none,
                  ),
                ),
              ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                height: 48,
                child: ElevatedButton(
                  onPressed: () async {
                    final reason = controller.text.trim();
                    if (reason.isEmpty) return;

                    Navigator.pop(context);
                    final success = await ref
                        .read(penaltyProvider.notifier)
                        .disputePenalty(penalty.id, reason);

                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(
                          content: Text(
                            success
                                ? 'Dispute submitted successfully.'
                                : 'Failed to submit dispute.',
                          ),
                          backgroundColor: success
                              ? AppTheme.accentSuccess
                              : AppTheme.accentCritical,
                        ),
                      );
                    }
                  },
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppTheme.primary,
                    foregroundColor: AppTheme.onPrimary,
                    elevation: 0,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppTheme.radiusXl),
                    ),
                  ),
                  child: Text(
                    'Submit Dispute',
                    style: AppTheme.labelLg.copyWith(
                      color: AppTheme.onPrimary,
                    ),
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
