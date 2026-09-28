import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mobile/main.dart';

void main() {
  testWidgets('App renders main navigation correctly',
      (WidgetTester tester) async {
    await tester.pumpWidget(const ProviderScope(child: OpenParkingApp()));
    expect(find.text('OPENPARKING PASS'), findsOneWidget);
    expect(find.text('Blueprint'), findsOneWidget);
    expect(find.text('Permit'), findsOneWidget);
  });
}
