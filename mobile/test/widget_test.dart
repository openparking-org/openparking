import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mobile/main.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUp(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
            const MethodChannel('plugins.it_nomads.com/flutter_secure_storage'),
            (_) async => null);
  });
  testWidgets('Signed-out customers see login and password recovery without QR',
      (WidgetTester tester) async {
    await tester
        .pumpWidget(ProviderScope(child: OpenParkingApp(theme: ThemeData())));
    await tester.pumpAndSettle();
    expect(find.text('Sign In'), findsWidgets);
    expect(find.text('Forgot Password?'), findsOneWidget);
    expect(find.text('OPENPARKING PASS'), findsNothing);
    await tester.tap(find.text('Forgot Password?'));
    await tester.pumpAndSettle();
    expect(find.text('Send reset email'), findsOneWidget);
  });
}
