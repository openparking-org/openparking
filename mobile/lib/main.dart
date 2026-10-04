import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_dotenv/flutter_dotenv.dart';
import 'core/router.dart';
import 'core/theme.dart';
import 'modules/home/home_dashboard_screen.dart';
import 'modules/space_availability/zone_discovery_screen.dart';
import 'modules/booking/booking_screen.dart';
import 'modules/account/account_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await dotenv.load(fileName: ".env").catchError((_) {});

  // Set system UI overlay style for monochrome theme
  SystemChrome.setSystemUIOverlayStyle(const SystemUiOverlayStyle(
    statusBarColor: Colors.transparent,
    statusBarIconBrightness: Brightness.dark,
    statusBarBrightness: Brightness.light,
    systemNavigationBarColor: AppTheme.surfacePure,
    systemNavigationBarIconBrightness: Brightness.dark,
  ));

  runApp(const ProviderScope(child: OpenParkingApp()));
}

class OpenParkingApp extends ConsumerWidget {
  final ThemeData? theme;
  const OpenParkingApp({super.key, this.theme});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);

    return MaterialApp.router(
      title: 'OpenParking',
      debugShowCheckedModeBanner: false,
      theme: theme ?? AppTheme.lightTheme,
      routerConfig: router,
    );
  }
}

class MainNavigationScreen extends StatefulWidget {
  const MainNavigationScreen({super.key});

  @override
  State<MainNavigationScreen> createState() => _MainNavigationScreenState();
}

class _MainNavigationScreenState extends State<MainNavigationScreen> {
  int _currentIndex = 0;

  final List<Widget> _screens = const [
    HomeDashboardScreen(),
    ZoneDiscoveryScreen(),
    BookingScreen(),
    AccountScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _currentIndex,
        children: _screens,
      ),
      bottomNavigationBar: DecoratedBox(
        decoration: const BoxDecoration(
          border: Border(top: BorderSide(color: AppTheme.borderSubtle)),
        ),
        child: NavigationBar(
          selectedIndex: _currentIndex,
          onDestinationSelected: (index) =>
              setState(() => _currentIndex = index),
          destinations: const [
            NavigationDestination(
                icon: Icon(Icons.local_parking_outlined),
                selectedIcon: Icon(Icons.local_parking),
                label: 'Home'),
            NavigationDestination(
                icon: Icon(Icons.search), label: 'Find Parking'),
            NavigationDestination(
                icon: Icon(Icons.calendar_today_outlined),
                selectedIcon: Icon(Icons.calendar_today),
                label: 'My Bookings'),
            NavigationDestination(
                icon: Icon(Icons.person_outline),
                selectedIcon: Icon(Icons.person),
                label: 'Account'),
          ],
        ),
      ),
    );
  }
}
