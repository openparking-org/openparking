import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import '../../core/theme.dart';
import '../../services/session_service.dart';

class GateScannerScreen extends StatefulWidget {
  final String bookingId;
  const GateScannerScreen({super.key, required this.bookingId});

  @override
  State<GateScannerScreen> createState() => _GateScannerScreenState();
}

class _GateScannerScreenState extends State<GateScannerScreen> {
  final MobileScannerController _controller = MobileScannerController();
  final SessionService _sessionService = SessionService();
  bool _isProcessing = false;

  void _onDetect(BarcodeCapture capture) async {
    if (_isProcessing) return;
    
    final List<Barcode> barcodes = capture.barcodes;
    if (barcodes.isEmpty) return;

    final String? rawValue = barcodes.first.rawValue;
    if (rawValue == null) return;

    setState(() => _isProcessing = true);

    try {
      final data = jsonDecode(rawValue);
      final gateId = data['gateId'];

      if (gateId != null) {
        await _sessionService.startSession(
          bookingId: widget.bookingId,
          gateId: gateId.toString(),
        );

        if (!mounted) return;
        _controller.stop();
        
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Gate opened successfully!'),
            backgroundColor: AppTheme.accentSuccess,
          ),
        );
        
        Navigator.of(context).pop(true);
      } else {
        throw Exception("Invalid QR Code: gateId missing.");
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Failed: ${e.toString().replaceAll('Exception: ', '')}'),
          backgroundColor: AppTheme.accentCritical,
        ),
      );
      Future.delayed(const Duration(seconds: 2), () {
        if (mounted) setState(() => _isProcessing = false);
      });
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        title: const Text('Scan Gate QR'),
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
      ),
      body: Stack(
        children: [
          MobileScanner(
            controller: _controller,
            onDetect: _onDetect,
          ),
          if (_isProcessing)
            Container(
              color: Colors.black54,
              child: const Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    CircularProgressIndicator(color: AppTheme.primary),
                    SizedBox(height: 16),
                    Text(
                      'Opening gate...',
                      style: TextStyle(color: Colors.white, fontSize: 16),
                    ),
                  ],
                ),
              ),
            ),
          // Simple scan window overlay
          Center(
            child: Container(
              width: 250,
              height: 250,
              decoration: BoxDecoration(
                border: Border.all(color: AppTheme.primary, width: 2),
                borderRadius: BorderRadius.circular(12),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
