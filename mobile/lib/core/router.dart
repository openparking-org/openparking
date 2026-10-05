import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'auth_provider.dart';
import '../main.dart'; // For MainNavigationScreen
import '../modules/user_access/login_screen.dart';

final routerProvider = Provider<GoRouter>((ref) {
  final authState = ref.watch(authProvider);

  final router = GoRouter(
    initialLocation: '/',
    redirect: (context, state) {
      if (authState.isLoading) return null;

      final isGoingToLogin = state.matchedLocation == '/login';
      if (!authState.isAuthenticated && !isGoingToLogin) {
        return '/login';
      }
      if (authState.isAuthenticated && isGoingToLogin) {
        return '/';
      }
      return null;
    },
    routes: [
      GoRoute(
        path: '/login',
        builder: (context, state) => const LoginScreen(),
      ),
      GoRoute(
        path: '/',
        builder: (context, state) {
          if (authState.isLoading) {
            return const Scaffold(
                body: Center(child: CircularProgressIndicator()));
          }
          return const MainNavigationScreen();
        },
      ),
    ],
  );
  ref.onDispose(router.dispose);
  return router;
});
