import 'dart:math' as math;
import 'package:flutter/material.dart';
import '../theme.dart';

/// Centers scrollable content on tablets while retaining phone-sized gutters.
EdgeInsets pagePadding(BuildContext context, {double vertical = 20}) {
  final width = MediaQuery.sizeOf(context).width;
  return EdgeInsets.symmetric(
    horizontal: math.max(16, (width - 720) / 2),
    vertical: vertical,
  );
}

class PageIntro extends StatelessWidget {
  final String title, subtitle;
  const PageIntro({super.key, required this.title, required this.subtitle});
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 24),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(title,
              style: AppTheme.headlineMd.copyWith(fontWeight: FontWeight.w700)),
          const SizedBox(height: 8),
          Text(subtitle,
              style: AppTheme.bodyMd.copyWith(color: AppTheme.textSecondary)),
        ]),
      );
}

class StatusBadge extends StatelessWidget {
  final String label;
  final Color? color;
  const StatusBadge(this.label, {super.key, this.color});
  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        decoration: BoxDecoration(
          color: color?.withValues(alpha: .08) ?? AppTheme.surfaceSubtle,
          borderRadius: BorderRadius.circular(24),
        ),
        child: Text(label,
            style: AppTheme.labelMd
                .copyWith(color: color ?? AppTheme.textSecondary)),
      );
}

class DetailLine extends StatelessWidget {
  final IconData icon;
  final String text;
  const DetailLine({super.key, required this.icon, required this.text});
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Icon(icon, size: 18, color: AppTheme.textTertiary),
          const SizedBox(width: 10),
          Expanded(
              child: Text(text,
                  style:
                      AppTheme.bodyMd.copyWith(color: AppTheme.textSecondary))),
        ]),
      );
}

class QuickAction extends StatelessWidget {
  final IconData icon;
  final String label;
  final VoidCallback onTap;
  const QuickAction(
      {super.key,
      required this.icon,
      required this.label,
      required this.onTap});
  @override
  Widget build(BuildContext context) => Material(
        color: AppTheme.surfacePure,
        shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
            side: const BorderSide(color: AppTheme.borderSubtle)),
        child: InkWell(
          borderRadius: BorderRadius.circular(12),
          onTap: onTap,
          child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(icon, size: 24),
                  const SizedBox(height: 16),
                  Text(label, style: AppTheme.labelLg),
                ],
              )),
        ),
      );
}
