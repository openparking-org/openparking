import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../core/providers/booking_provider.dart';
import '../../core/providers/zone_provider.dart';
import '../../core/providers/permit_provider.dart';
import '../../models/slot.dart';
import '../space_availability/indoor_map_screen.dart';

class CreateBookingScreen extends ConsumerStatefulWidget {
  final String zoneId;
  final String? preselectedSlotId;

  const CreateBookingScreen({
    super.key,
    required this.zoneId,
    this.preselectedSlotId,
  });

  @override
  ConsumerState<CreateBookingScreen> createState() =>
      _CreateBookingScreenState();
}

class _CreateBookingScreenState extends ConsumerState<CreateBookingScreen> {
  final _formKey = GlobalKey<FormState>();
  final _plateController = TextEditingController();

  SlotModel? _selectedSlot;
  DateTime _startTime = DateTime.now();
  DateTime _endTime = DateTime.now().add(const Duration(hours: 2));
  final bool _applyDisabilityDiscount = false;

  @override
  void dispose() {
    _plateController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final zoneState = ref.watch(zoneDetailProvider(widget.zoneId));
    final bookingState = ref.watch(bookingProvider);
    final permitState = ref.watch(permitProvider);

    final zone = zoneState.asData?.value;
    final availableSlots =
        zone?.slots.where((s) => s.isAvailable).toList() ?? [];

    if (_selectedSlot == null && availableSlots.isNotEmpty) {
      if (widget.preselectedSlotId != null) {
        _selectedSlot = availableSlots.firstWhere(
          (s) => s.id == widget.preselectedSlotId,
          orElse: () => availableSlots.first,
        );
      } else {
        _selectedSlot = availableSlots.first;
      }
    }

    final durationHours = _endTime.difference(_startTime).inMinutes / 60.0;
    final baseRate = zone?.baseHourlyRate ?? 5.0;
    double estimatedFee = durationHours > 0 ? durationHours * baseRate : 0.0;
    if (_applyDisabilityDiscount ||
        (permitState.permit?.status.toLowerCase() == 'verified')) {
      estimatedFee *= 0.85; // 15% disability discount
    }

    return Scaffold(
      backgroundColor: const Color(0xFF0B0F19),
      appBar: AppBar(
        title: const Text('New Reservation',
            style: TextStyle(fontWeight: FontWeight.bold)),
        backgroundColor: const Color(0xFF111827),
        elevation: 0,
      ),
      body: zoneState.isLoading
          ? const Center(
              child: CircularProgressIndicator(color: Color(0xFF6366F1)))
          : zoneState.hasError
              ? Center(
                  child: Column(mainAxisSize: MainAxisSize.min, children: [
                    Text('${zoneState.error}',
                        style: const TextStyle(color: Colors.white)),
                    TextButton(
                      onPressed: () =>
                          ref.invalidate(zoneDetailProvider(widget.zoneId)),
                      child: const Text('Retry'),
                    ),
                  ]),
                )
              : SingleChildScrollView(
                  padding: const EdgeInsets.all(20),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        // Zone Header Info Card
                        Container(
                          padding: const EdgeInsets.all(16),
                          decoration: BoxDecoration(
                            color: const Color(0xFF1E293B),
                            borderRadius: BorderRadius.circular(16),
                            border: Border.all(color: const Color(0xFF334155)),
                          ),
                          child: Row(
                            children: [
                              const Icon(Icons.local_parking,
                                  color: Color(0xFF6366F1), size: 36),
                              const SizedBox(width: 14),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      zone?.name ?? 'Loading Zone...',
                                      style: const TextStyle(
                                        fontSize: 18,
                                        fontWeight: FontWeight.bold,
                                        color: Colors.white,
                                      ),
                                    ),
                                    const SizedBox(height: 2),
                                    Text(
                                      'Rate: ${zone?.currency ?? "USD"} ${baseRate.toStringAsFixed(2)}/hr',
                                      style: const TextStyle(
                                          color: Colors.white70, fontSize: 13),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 24),

                        // Slot Picker
                        const Text(
                          'Select Slot',
                          style: TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.bold,
                              fontSize: 16),
                        ),
                        const SizedBox(height: 8),
                        DropdownButtonFormField<SlotModel>(
                          initialValue: _selectedSlot,
                          dropdownColor: const Color(0xFF1E293B),
                          decoration: InputDecoration(
                            filled: true,
                            fillColor: const Color(0xFF1E293B),
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12),
                              borderSide:
                                  const BorderSide(color: Color(0xFF334155)),
                            ),
                          ),
                          items: availableSlots.map((slot) {
                            return DropdownMenuItem<SlotModel>(
                              value: slot,
                              child: Text(
                                'Slot ${slot.slotNumber} (${slot.type}) - Floor ${slot.floor}',
                                style: const TextStyle(color: Colors.white),
                              ),
                            );
                          }).toList(),
                          onChanged: (val) {
                            setState(() => _selectedSlot = val);
                          },
                        ),
                        const SizedBox(height: 20),

                        // Start & End Time Pickers
                        Row(
                          children: [
                            Expanded(
                              child: _buildTimePickerTile(
                                context,
                                title: 'Start Time',
                                time: _startTime,
                                onTap: () =>
                                    _pickDateTime(context, isStart: true),
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: _buildTimePickerTile(
                                context,
                                title: 'End Time',
                                time: _endTime,
                                onTap: () =>
                                    _pickDateTime(context, isStart: false),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 20),

                        // Vehicle Plate Field
                        const Text(
                          'Vehicle License Plate',
                          style: TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.bold,
                              fontSize: 16),
                        ),
                        const SizedBox(height: 8),
                        TextFormField(
                          controller: _plateController,
                          style: const TextStyle(color: Colors.white),
                          decoration: InputDecoration(
                            hintText: 'e.g. ABC-1234',
                            hintStyle: const TextStyle(color: Colors.white38),
                            prefixIcon: const Icon(Icons.directions_car,
                                color: Color(0xFF6366F1)),
                            filled: true,
                            fillColor: const Color(0xFF1E293B),
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12),
                              borderSide:
                                  const BorderSide(color: Color(0xFF334155)),
                            ),
                          ),
                          validator: (val) => val == null || val.trim().isEmpty
                              ? 'Please enter vehicle plate'
                              : null,
                        ),
                        const SizedBox(height: 20),

                        // Disability Discount Check
                        if (permitState.permit != null &&
                            permitState.permit!.status.toLowerCase() ==
                                'verified')
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: Colors.blueAccent.withValues(alpha: 0.15),
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(
                                  color:
                                      Colors.blueAccent.withValues(alpha: 0.4)),
                            ),
                            child: const Row(
                              children: [
                                Icon(Icons.accessible,
                                    color: Colors.blueAccent),
                                SizedBox(width: 10),
                                Expanded(
                                  child: Text(
                                    '15% Disability Discount Automatically Applied',
                                    style: TextStyle(
                                        color: Colors.blueAccent,
                                        fontWeight: FontWeight.bold),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        const SizedBox(height: 24),

                        // Estimated Fee Summary Card
                        Container(
                          padding: const EdgeInsets.all(16),
                          decoration: BoxDecoration(
                            color: const Color(0xFF111827),
                            borderRadius: BorderRadius.circular(16),
                            border: Border.all(color: const Color(0xFF334155)),
                          ),
                          child: Column(
                            children: [
                              Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('Duration',
                                      style: TextStyle(color: Colors.white60)),
                                  Text(
                                      '${durationHours.toStringAsFixed(1)} hrs',
                                      style: const TextStyle(
                                          color: Colors.white,
                                          fontWeight: FontWeight.bold)),
                                ],
                              ),
                              const Divider(
                                  color: Color(0xFF334155), height: 20),
                              Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text(
                                    'Estimated Total',
                                    style: TextStyle(
                                        color: Colors.white,
                                        fontSize: 16,
                                        fontWeight: FontWeight.bold),
                                  ),
                                  Text(
                                    '${zone?.currency ?? "USD"} ${estimatedFee.toStringAsFixed(2)}',
                                    style: const TextStyle(
                                      color: Color(0xFF10B981),
                                      fontSize: 20,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 28),

                        // Confirm Booking Action Button
                        SizedBox(
                          width: double.infinity,
                          child: ElevatedButton(
                            onPressed:
                                bookingState.isCreating ? null : _submitBooking,
                            style: ElevatedButton.styleFrom(
                              backgroundColor: const Color(0xFF6366F1),
                              foregroundColor: Colors.white,
                              padding: const EdgeInsets.symmetric(vertical: 16),
                              shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(12)),
                            ),
                            child: bookingState.isCreating
                                ? const CircularProgressIndicator(
                                    color: Colors.white)
                                : const Text(
                                    'Confirm & Reserve',
                                    style: TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.bold),
                                  ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
    );
  }

  Widget _buildTimePickerTile(
    BuildContext context, {
    required String title,
    required DateTime time,
    required VoidCallback onTap,
  }) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: const Color(0xFF1E293B),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: const Color(0xFF334155)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title,
                style: const TextStyle(color: Colors.white54, fontSize: 12)),
            const SizedBox(height: 4),
            Row(
              children: [
                const Icon(Icons.access_time,
                    color: Color(0xFF6366F1), size: 18),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    DateFormat('MMM d, HH:mm').format(time),
                    style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 13),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _pickDateTime(BuildContext context,
      {required bool isStart}) async {
    final initial = isStart ? _startTime : _endTime;
    final date = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime.now().subtract(const Duration(hours: 1)),
      lastDate: DateTime.now().add(const Duration(days: 30)),
    );

    if (date == null || !context.mounted) return;

    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initial),
    );

    if (time == null || !mounted) return;

    final selected =
        DateTime(date.year, date.month, date.day, time.hour, time.minute);

    setState(() {
      if (isStart) {
        _startTime = selected;
        if (_endTime.isBefore(_startTime)) {
          _endTime = _startTime.add(const Duration(hours: 2));
        }
      } else {
        _endTime = selected;
      }
    });
  }

  Future<void> _submitBooking() async {
    if (!_formKey.currentState!.validate() || _selectedSlot == null) return;
    if (!_endTime.isAfter(_startTime) || _endTime.isBefore(DateTime.now())) {
      ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Choose a valid start and end time.')));
      return;
    }

    final booking = await ref.read(bookingProvider.notifier).createBooking(
          slotId: _selectedSlot!.id,
          startTime: _startTime.toUtc(),
          endTime: _endTime.toUtc(),
          vehiclePlate: _plateController.text.trim(),
        );

    if (!mounted) return;
    if (booking == null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text(ref.read(bookingProvider).error ??
              'Could not reserve this space.')));
      return;
    }
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content:
              Text('Reserved! Give your vehicle number to the gate attendant.'),
          backgroundColor: Color(0xFF10B981),
        ),
      );

      Navigator.pushReplacement(
        context,
        MaterialPageRoute(
          builder: (context) => IndoorMapScreen(
            zoneId: widget.zoneId,
            targetSlotId: booking.slotId,
            bookingId: booking.id,
          ),
        ),
      );
    }
  }
}
