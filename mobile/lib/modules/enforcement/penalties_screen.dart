import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../core/providers/penalty_provider.dart';
import '../../models/penalty.dart';

class PenaltiesScreen extends ConsumerWidget {
  const PenaltiesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final penaltyState = ref.watch(penaltyProvider);

    return Scaffold(
      backgroundColor: const Color(0xFF0B0F19),
      appBar: AppBar(
        title: const Text('Enforcement & Penalties', style: TextStyle(fontWeight: FontWeight.bold)),
        backgroundColor: const Color(0xFF111827),
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.read(penaltyProvider.notifier).fetchPenalties(),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async => ref.read(penaltyProvider.notifier).fetchPenalties(),
        color: const Color(0xFF6366F1),
        backgroundColor: const Color(0xFF1E293B),
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // AI Overstay Banner
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: const Color(0x1FEF4444),
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: const Color(0x66EF4444)),
                ),
                child: const Row(
                  children: [
                    Icon(Icons.warning_amber_rounded, color: Color(0xFFEF4444), size: 32),
                    SizedBox(width: 14),
                    Expanded(
                      child: Text(
                        'AI Overstay Detection active across all zones. 15-minute grace period strictly enforced.',
                        style: TextStyle(color: Colors.white, fontSize: 13, height: 1.4),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),

              const Text(
                'Issued Penalties',
                style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 12),

              if (penaltyState.isLoading)
                const Padding(
                  padding: EdgeInsets.all(32),
                  child: Center(
                    child: CircularProgressIndicator(color: Color(0xFF6366F1)),
                  ),
                )
              else if (penaltyState.penalties.isEmpty)
                _buildEmptyState()
              else
                ListView.builder(
                  shrinkWrap: true,
                  physics: const NeverScrollableScrollPhysics(),
                  itemCount: penaltyState.penalties.length,
                  itemBuilder: (context, index) {
                    final penalty = penaltyState.penalties[index];
                    return _buildPenaltyCard(context, ref, penalty);
                  },
                ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildEmptyState() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(32),
      decoration: BoxDecoration(
        color: const Color(0xFF1E293B),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFF334155)),
      ),
      child: const Column(
        children: [
          Icon(Icons.verified_user_outlined, size: 48, color: Color(0xFF10B981)),
          SizedBox(height: 12),
          Text(
            'Clean Enforcement Record',
            style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 16),
          ),
          SizedBox(height: 4),
          Text(
            'No penalties or active disputes found.',
            style: TextStyle(color: Colors.white54, fontSize: 13),
          ),
        ],
      ),
    );
  }

  Widget _buildPenaltyCard(BuildContext context, WidgetRef ref, PenaltyModel penalty) {
    Color statusColor;
    switch (penalty.status.toLowerCase()) {
      case 'approved':
      case 'pending':
        statusColor = Colors.redAccent;
        break;
      case 'disputed':
        statusColor = Colors.orangeAccent;
        break;
      case 'waived':
      case 'paid':
        statusColor = const Color(0xFF10B981);
        break;
      default:
        statusColor = Colors.grey;
    }

    return Card(
      margin: const EdgeInsets.only(bottom: 16),
      color: const Color(0xFF1E293B),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      elevation: 3,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    const Icon(Icons.receipt_long, color: Color(0xFF6366F1), size: 20),
                    const SizedBox(width: 8),
                    Text(
                      penalty.reason,
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                  ],
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: statusColor.withOpacity(0.2),
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: statusColor.withOpacity(0.5)),
                  ),
                  child: Text(
                    penalty.status,
                    style: TextStyle(
                      color: statusColor,
                      fontWeight: FontWeight.bold,
                      fontSize: 12,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),

            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Issued On', style: TextStyle(color: Colors.white54, fontSize: 12)),
                    const SizedBox(height: 2),
                    Text(
                      DateFormat('MMM d, yyyy - HH:mm').format(penalty.issuedAt),
                      style: const TextStyle(color: Colors.white70, fontSize: 13),
                    ),
                  ],
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    const Text('Fine Amount', style: TextStyle(color: Colors.white54, fontSize: 12)),
                    const SizedBox(height: 2),
                    Text(
                      'USD ${penalty.amount.toStringAsFixed(2)}',
                      style: const TextStyle(
                        color: Colors.redAccent,
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                  ],
                ),
              ],
            ),

            if (penalty.disputeNotes != null && penalty.disputeNotes!.isNotEmpty) ...[
              const SizedBox(height: 12),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: const Color(0xFF111827),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Text(
                  'Dispute Notes: ${penalty.disputeNotes}',
                  style: const TextStyle(color: Colors.white70, fontSize: 12),
                ),
              ),
            ],

            const SizedBox(height: 16),
            if (penalty.isDisputable)
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: () => _showDisputeDialog(context, ref, penalty),
                  icon: const Icon(Icons.gavel, size: 18),
                  label: const Text('Dispute Penalty (7-Day Window)'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: Colors.orangeAccent,
                    side: const BorderSide(color: Colors.orangeAccent),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  void _showDisputeDialog(BuildContext context, WidgetRef ref, PenaltyModel penalty) {
    final controller = TextEditingController();

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: const Color(0xFF1E293B),
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
              const Text(
                'Dispute Penalty Fine',
                style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold, color: Colors.white),
              ),
              const SizedBox(height: 8),
              const Text(
                'Please explain the reason for your dispute. Parking admins will review your request.',
                style: TextStyle(color: Colors.white60, fontSize: 13),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: controller,
                maxLines: 4,
                style: const TextStyle(color: Colors.white),
                decoration: InputDecoration(
                  hintText: 'Enter dispute reason (e.g., machine malfunction, emergency)...',
                  hintStyle: const TextStyle(color: Colors.white38),
                  filled: true,
                  fillColor: const Color(0xFF111827),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12),
                    borderSide: const BorderSide(color: Color(0xFF334155)),
                  ),
                ),
              ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
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
                            success ? 'Dispute submitted successfully.' : 'Failed to submit dispute.',
                          ),
                          backgroundColor: success ? const Color(0xFF10B981) : Colors.redAccent,
                        ),
                      );
                    }
                  },
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.orangeAccent,
                    foregroundColor: Colors.black,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                  child: const Text('Submit Dispute', style: TextStyle(fontWeight: FontWeight.bold)),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}
