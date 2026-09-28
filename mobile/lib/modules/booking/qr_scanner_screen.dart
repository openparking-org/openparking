import 'dart:async';
import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import '../../services/session_service.dart';
import '../../utils/qr_payload_parser.dart';

enum QrScannerMode {
  entryCheckIn,
  exitCheckOut,
  autoDetect,
}

class QrScannerScreen extends StatefulWidget {
  final QrScannerMode mode;
  final String? existingBookingId;
  final String? existingSessionId;

  const QrScannerScreen({
    super.key,
    this.mode = QrScannerMode.autoDetect,
    this.existingBookingId,
    this.existingSessionId,
  });

  @override
  State<QrScannerScreen> createState() => _QrScannerScreenState();
}

class _QrScannerScreenState extends State<QrScannerScreen>
    with WidgetsBindingObserver {
  late final MobileScannerController _scannerController;
  final SessionService _sessionService = SessionService();

  bool _isProcessing = false;
  bool _isTorchOn = false;
  String? _lastScannedValue;
  DateTime? _lastScannedTime;

  String? _errorMessage;
  ParkingSessionModel? _successfulSession;
  String? _successActionType;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _scannerController = MobileScannerController(
      detectionSpeed: DetectionSpeed.noDuplicates,
      facing: CameraFacing.back,
      torchEnabled: false,
    );
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (!mounted) return;
    if (state == AppLifecycleState.resumed) {
      if (!_isProcessing && _successfulSession == null) {
        _scannerController.start();
      }
    } else if (state == AppLifecycleState.inactive ||
        state == AppLifecycleState.paused) {
      _scannerController.stop();
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _scannerController.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_isProcessing || _successfulSession != null) return;

    final barcode = capture.barcodes.firstOrNull;
    if (barcode == null) return;

    final rawValue = barcode.rawValue ?? barcode.displayValue;
    if (rawValue == null || rawValue.trim().isEmpty) return;

    // Duplicate scan protection: Debounce rapid repeated captures within 3 seconds
    final now = DateTime.now();
    if (_lastScannedValue == rawValue &&
        _lastScannedTime != null &&
        now.difference(_lastScannedTime!).inSeconds < 3) {
      return;
    }

    _lastScannedValue = rawValue;
    _lastScannedTime = now;

    _handleScannedData(rawValue);
  }

  Future<void> _handleScannedData(String rawValue) async {
    setState(() {
      _isProcessing = true;
      _errorMessage = null;
    });

    // 1. Validate and parse QR payload
    final parseResult = QrPayloadParser.parse(rawValue);
    if (!parseResult.isValid) {
      setState(() {
        _isProcessing = false;
        _errorMessage = parseResult.errorMessage ??
            'Invalid or unsupported QR code format.';
      });
      return;
    }

    // 2. Determine whether this is a check-in or check-out operation
    final isCheckOut = widget.mode == QrScannerMode.exitCheckOut ||
        (widget.mode == QrScannerMode.autoDetect &&
            parseResult.action == QrActionType.checkOut);

    try {
      ParkingSessionModel session;

      if (isCheckOut) {
        final sessionId = parseResult.sessionId ?? widget.existingSessionId;
        final bookingId = parseResult.bookingId ?? widget.existingBookingId;

        if (sessionId == null && bookingId == null) {
          throw SessionApiException(400,
              'Check-out QR requires an active session or booking reference.');
        }

        session = await _sessionService.checkOut(
          sessionId: sessionId,
          bookingId: bookingId,
        );
        _successActionType = 'Check-Out Complete';
      } else {
        final bookingId = parseResult.bookingId ?? widget.existingBookingId;
        final slotId = parseResult.slotId;

        session = await _sessionService.checkIn(
          bookingId: bookingId,
          slotId: slotId,
        );
        _successActionType = 'Gate Barrier Open — Check-In Confirmed';
      }

      if (mounted) {
        setState(() {
          _isProcessing = false;
          _successfulSession = session;
        });
      }
    } on SessionApiException catch (e) {
      if (mounted) {
        setState(() {
          _isProcessing = false;
          _errorMessage = e.message;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _isProcessing = false;
          _errorMessage =
              'An unexpected error occurred while processing scan: $e';
        });
      }
    }
  }

  void _resetScanner() {
    setState(() {
      _errorMessage = null;
      _successfulSession = null;
      _isProcessing = false;
      _lastScannedValue = null;
      _lastScannedTime = null;
    });
    _scannerController.start();
  }

  void _finishAndReturn(ParkingSessionModel? session) {
    Navigator.of(context).pop(session);
  }

  @override
  Widget build(BuildContext context) {
    final titleText = widget.mode == QrScannerMode.entryCheckIn
        ? 'Scan Entry Gate QR'
        : widget.mode == QrScannerMode.exitCheckOut
            ? 'Scan Exit Gate QR'
            : 'Scan Gate Terminal QR';

    return Scaffold(
      backgroundColor: const Color(0xFF0B0F19),
      appBar: AppBar(
        title: Text(titleText,
            style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 18)),
        backgroundColor: const Color(0xFF111827),
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => _finishAndReturn(null),
        ),
        actions: [
          IconButton(
            icon: Icon(_isTorchOn ? Icons.flash_on : Icons.flash_off),
            color: _isTorchOn ? const Color(0xFFF59E0B) : Colors.white70,
            tooltip: 'Toggle Flashlight',
            onPressed: () async {
              await _scannerController.toggleTorch();
              setState(() => _isTorchOn = !_isTorchOn);
            },
          ),
          IconButton(
            icon: const Icon(Icons.flip_camera_ios),
            tooltip: 'Switch Camera',
            onPressed: () => _scannerController.switchCamera(),
          ),
        ],
      ),
      body: Stack(
        children: [
          // 1. Mobile Camera Viewfinder
          MobileScanner(
            controller: _scannerController,
            onDetect: _onDetect,
            errorBuilder: (context, error, child) {
              return Center(
                child: Padding(
                  padding: const EdgeInsets.all(24.0),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(Icons.videocam_off_outlined,
                          color: Color(0xFFEF4444), size: 48),
                      const SizedBox(height: 16),
                      const Text(
                        'Camera Permission Required',
                        style: TextStyle(
                            color: Colors.white,
                            fontSize: 18,
                            fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        'Please allow camera access in device settings to scan parking gate QR codes.\n(${error.errorDetails?.message ?? error.errorCode})',
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                            color: Colors.white70, fontSize: 13),
                      ),
                      const SizedBox(height: 20),
                      ElevatedButton.icon(
                        style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFF6366F1)),
                        icon: const Icon(Icons.refresh),
                        label: const Text('Retry Camera'),
                        onPressed: () => _scannerController.start(),
                      )
                    ],
                  ),
                ),
              );
            },
          ),

          // 2. Scan Reticle Overlay
          _buildScannerOverlay(),

          // 3. Processing Spinner Overlay
          if (_isProcessing)
            Container(
              color: const Color(0xBF000000),
              child: const Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    CircularProgressIndicator(
                      valueColor:
                          AlwaysStoppedAnimation<Color>(Color(0xFF6366F1)),
                      strokeWidth: 3,
                    ),
                    SizedBox(height: 20),
                    Text(
                      'Verifying Gate Terminal QR...',
                      style: TextStyle(
                          color: Colors.white,
                          fontSize: 16,
                          fontWeight: FontWeight.w600),
                    ),
                    SizedBox(height: 6),
                    Text(
                      'Validating booking and activating session...',
                      style: TextStyle(color: Colors.white70, fontSize: 13),
                    ),
                  ],
                ),
              ),
            ),

          // 4. Error Sheet
          if (_errorMessage != null) _buildErrorOverlay(_errorMessage!),

          // 5. Success Dialog/Card
          if (_successfulSession != null)
            _buildSuccessOverlay(_successfulSession!),
        ],
      ),
    );
  }

  Widget _buildScannerOverlay() {
    return LayoutBuilder(
      builder: (context, constraints) {
        final scanAreaSize = constraints.maxWidth * 0.72;

        return Stack(
          children: [
            // Dark vignette mask with cutout
            ColorFiltered(
              colorFilter: const ColorFilter.mode(
                Color(0x8C000000),
                BlendMode.srcOut,
              ),
              child: Stack(
                fit: StackFit.expand,
                children: [
                  Container(
                    decoration: const BoxDecoration(
                      color: Colors.black,
                      backgroundBlendMode: BlendMode.dstOut,
                    ),
                  ),
                  Align(
                    alignment: Alignment.center,
                    child: Container(
                      width: scanAreaSize,
                      height: scanAreaSize,
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(16),
                      ),
                    ),
                  ),
                ],
              ),
            ),

            // Neon reticle borders
            Center(
              child: Container(
                width: scanAreaSize,
                height: scanAreaSize,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: const Color(0xFF6366F1), width: 2),
                ),
              ),
            ),

            // Instructional banner at bottom
            Positioned(
              left: 20,
              right: 20,
              bottom: 40,
              child: Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                decoration: BoxDecoration(
                  color: const Color(0xEB1E293B),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: const Color(0x336366F1)),
                ),
                child: const Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.qr_code_scanner,
                        color: Color(0xFF6366F1), size: 20),
                    SizedBox(width: 10),
                    Flexible(
                      child: Text(
                        'Align terminal QR code within the frame',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                            color: Colors.white,
                            fontSize: 13,
                            fontWeight: FontWeight.w500),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        );
      },
    );
  }

  Widget _buildErrorOverlay(String message) {
    return Container(
      color: const Color(0xD9000000),
      padding: const EdgeInsets.all(28.0),
      child: Center(
        child: Container(
          padding: const EdgeInsets.all(24.0),
          decoration: BoxDecoration(
            color: const Color(0xFF1E293B),
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: const Color(0x66EF4444)),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline_rounded,
                  color: Color(0xFFEF4444), size: 48),
              const SizedBox(height: 16),
              const Text(
                'Scan Unsuccessful',
                style: TextStyle(
                    color: Colors.white,
                    fontSize: 18,
                    fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              Text(
                message,
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.white70, fontSize: 14),
              ),
              const SizedBox(height: 24),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                children: [
                  OutlinedButton(
                    style: OutlinedButton.styleFrom(
                      foregroundColor: Colors.white70,
                      side: const BorderSide(color: Colors.white24),
                    ),
                    onPressed: () => _finishAndReturn(null),
                    child: const Text('Cancel'),
                  ),
                  ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF6366F1),
                    ),
                    icon: const Icon(Icons.refresh),
                    label: const Text('Scan Again'),
                    onPressed: _resetScanner,
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildSuccessOverlay(ParkingSessionModel session) {
    final isExit = session.status == 'Completed';

    return Container(
      color: const Color(0xD9000000),
      padding: const EdgeInsets.all(24.0),
      child: Center(
        child: Container(
          padding: const EdgeInsets.all(24.0),
          decoration: BoxDecoration(
            color: const Color(0xFF1E293B),
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: const Color(0x6610B981)),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.check_circle_rounded,
                  color: Color(0xFF10B981), size: 54),
              const SizedBox(height: 14),
              Text(
                _successActionType ?? 'Scan Successful',
                textAlign: TextAlign.center,
                style: const TextStyle(
                    color: Colors.white,
                    fontSize: 18,
                    fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              Text(
                isExit
                    ? 'Session concluded successfully. Gate barrier opening.'
                    : 'Barrier opening. Please proceed to your allocated bay.',
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.white70, fontSize: 13),
              ),
              const SizedBox(height: 18),

              // Details card
              Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: Colors.black26,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Column(
                  children: [
                    _detailRow('Zone & Bay',
                        '${session.zoneName} · Bay ${session.slotNumber}'),
                    const SizedBox(height: 6),
                    _detailRow('Status', session.status,
                        valueColor: const Color(0xFF10B981)),
                    if (isExit) ...[
                      const SizedBox(height: 6),
                      _detailRow('Total Fee',
                          '\$${session.totalFee.toStringAsFixed(2)}',
                          isBold: true),
                      if (session.penaltyFee > 0) ...[
                        const SizedBox(height: 6),
                        _detailRow('Overstay Fine',
                            '\$${session.penaltyFee.toStringAsFixed(2)}',
                            valueColor: const Color(0xFFEF4444)),
                      ],
                    ] else ...[
                      const SizedBox(height: 6),
                      _detailRow(
                          'Check-in Time', _formatTime(session.checkInTime)),
                      const SizedBox(height: 6),
                      _detailRow('Rate',
                          '\$${session.hourlyRate.toStringAsFixed(2)}/hr'),
                    ],
                  ],
                ),
              ),

              const SizedBox(height: 22),
              ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF10B981),
                  minimumSize: const Size.fromHeight(44),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10)),
                ),
                onPressed: () => _finishAndReturn(session),
                child: Text(
                  isExit ? 'Return to Home' : 'Proceed to Active Session',
                  style: const TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 15,
                      color: Colors.white),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _detailRow(String label, String value,
      {Color? valueColor, bool isBold = false}) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: const TextStyle(color: Colors.grey, fontSize: 13)),
        Text(
          value,
          style: TextStyle(
            color: valueColor ?? Colors.white,
            fontWeight: isBold ? FontWeight.bold : FontWeight.w600,
            fontSize: 13,
          ),
        ),
      ],
    );
  }

  String _formatTime(DateTime dt) {
    final hour = dt.hour.toString().padLeft(2, '0');
    final minute = dt.minute.toString().padLeft(2, '0');
    return '$hour:$minute';
  }
}
