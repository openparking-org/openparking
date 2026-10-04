import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/auth_provider.dart';
import '../../core/theme.dart';
import '../../services/user_service.dart';
import 'password_recovery_screen.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _isLoading = false;
  bool _remember = true;
  bool _obscurePassword = true;

  // Registration state
  bool _isRegisterMode = false;
  final _nameController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _nameController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final email = _emailController.text.trim();
    final password = _passwordController.text;

    if (email.isEmpty || password.isEmpty) {
      _showSnackBar('Please fill in all fields', isError: true);
      return;
    }

    setState(() => _isLoading = true);

    try {
      if (_isRegisterMode) {
        final name = _nameController.text.trim();
        if (name.isEmpty) {
          _showSnackBar('Please enter your full name', isError: true);
          setState(() => _isLoading = false);
          return;
        }

        final userService = UserService();
        await userService.register(
            fullName: name, email: email, password: password);
        _showSnackBar('Account created successfully! Logging in...');
      }

      final success = await ref
          .read(authProvider.notifier)
          .login(email, password, remember: _remember);

      if (!mounted) return;
      setState(() => _isLoading = false);

      if (!success) {
        _showSnackBar(ref.read(authProvider).error ?? 'Authentication failed.',
            isError: true);
      }
    } catch (e) {
      if (!mounted) return;
      setState(() => _isLoading = false);
      _showSnackBar(e.toString().replaceAll('Exception: ', ''), isError: true);
    }
  }

  void _showSnackBar(String text, {bool isError = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(text),
        backgroundColor:
            isError ? AppTheme.accentCritical : AppTheme.accentSuccess,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppTheme.surface,
      body: SafeArea(
        child: Column(
          children: [
            // ── TOP BAR ──
            Container(
              height: 56,
              padding: const EdgeInsets.symmetric(horizontal: AppTheme.margin),
              decoration: const BoxDecoration(
                color: AppTheme.surfacePure,
                border: Border(
                  bottom: BorderSide(color: AppTheme.borderSubtle),
                ),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  IconButton(
                    icon: const Icon(Icons.arrow_back, size: 20),
                    color: AppTheme.textPrimary,
                    onPressed: () => showDialog<void>(
                        context: context,
                        builder: (_) => const AlertDialog(
                            title: Text('OpenParking'),
                            content: Text(
                                'Reserve parking, give your vehicle number at the gate, and view your session and receipts in My Bookings.'))),
                    padding: EdgeInsets.zero,
                    constraints:
                        const BoxConstraints(minWidth: 40, minHeight: 40),
                  ),
                  Row(
                    children: [
                      Text(
                        'OpenParking',
                        style: AppTheme.titleMd.copyWith(
                          fontWeight: FontWeight.w700,
                          letterSpacing: -0.3,
                        ),
                      ),
                      const SizedBox(width: 4),
                      Container(
                        width: 6,
                        height: 6,
                        decoration: const BoxDecoration(
                          color: AppTheme.primary,
                          shape: BoxShape.circle,
                        ),
                      ),
                    ],
                  ),
                  IconButton(
                    icon: const Icon(Icons.help_outline, size: 20),
                    color: AppTheme.textSecondary,
                    onPressed: () {},
                    padding: EdgeInsets.zero,
                    constraints:
                        const BoxConstraints(minWidth: 40, minHeight: 40),
                  ),
                ],
              ),
            ),

            // ── FORM CONTENT ──
            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.symmetric(
                  horizontal: AppTheme.margin,
                  vertical: AppTheme.spaceLg,
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Badge
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 10, vertical: 6),
                      decoration: BoxDecoration(
                        color: AppTheme.surfaceSubtle,
                        borderRadius: BorderRadius.circular(100),
                        border: Border.all(color: AppTheme.borderSubtle),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(Icons.local_parking,
                              size: 14, color: AppTheme.textPrimary),
                          const SizedBox(width: 4),
                          Text(
                            'DRIVER & FLEET PORTAL',
                            style: AppTheme.labelSm.copyWith(
                              color: AppTheme.textSecondary,
                              letterSpacing: 0.8,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: AppTheme.spaceSm),

                    // Title
                    Text(
                      _isRegisterMode ? 'Create Account' : 'Welcome Back',
                      style: AppTheme.headlineSm,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      _isRegisterMode
                          ? 'Register to access smart parking services.'
                          : 'Sign in to access your parking reservations, bookings, sessions, and receipts.',
                      style: AppTheme.bodyMd
                          .copyWith(color: AppTheme.textSecondary),
                    ),

                    const SizedBox(height: AppTheme.spaceLg),

                    // ── NAME FIELD (Register mode only) ──
                    if (_isRegisterMode) ...[
                      const Text('Full Name', style: AppTheme.labelMd),
                      const SizedBox(height: 6),
                      TextField(
                        controller: _nameController,
                        style: AppTheme.bodyMd,
                        decoration: const InputDecoration(
                          hintText: 'John Doe',
                          prefixIcon: Icon(Icons.person_outline,
                              color: AppTheme.textTertiary, size: 18),
                        ),
                      ),
                      const SizedBox(height: AppTheme.spaceMd),
                    ],

                    // ── EMAIL FIELD ──
                    const Text('Email Address', style: AppTheme.labelMd),
                    const SizedBox(height: 6),
                    TextField(
                      controller: _emailController,
                      keyboardType: TextInputType.emailAddress,
                      style: AppTheme.bodyMd,
                      decoration: const InputDecoration(
                        hintText: 'name@domain.com',
                        prefixIcon: Icon(
                          Icons.mail_outline,
                          color: AppTheme.textTertiary,
                          size: 18,
                        ),
                      ),
                    ),
                    const SizedBox(height: AppTheme.spaceMd),

                    // ── PASSWORD FIELD ──
                    const Text('Password', style: AppTheme.labelMd),
                    const SizedBox(height: 6),
                    TextField(
                      controller: _passwordController,
                      obscureText: _obscurePassword,
                      style: AppTheme.bodyMd,
                      decoration: InputDecoration(
                        hintText: '••••••••••••',
                        suffixIcon: IconButton(
                          icon: Icon(
                            _obscurePassword
                                ? Icons.visibility
                                : Icons.visibility_off,
                            color: AppTheme.textSecondary,
                            size: 18,
                          ),
                          onPressed: () => setState(
                              () => _obscurePassword = !_obscurePassword),
                        ),
                      ),
                    ),

                    const SizedBox(height: AppTheme.spaceSm),

                    // Remember & Forgot
                    if (!_isRegisterMode)
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Row(
                            children: [
                              SizedBox(
                                width: 18,
                                height: 18,
                                child: Checkbox(
                                  value: _remember,
                                  onChanged: (v) =>
                                      setState(() => _remember = v ?? true),
                                  activeColor: AppTheme.primary,
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(4),
                                  ),
                                  side: const BorderSide(
                                      color: AppTheme.borderStrong),
                                ),
                              ),
                              const SizedBox(width: 8),
                              Text(
                                'Remember this device',
                                style: AppTheme.bodySm.copyWith(
                                  color: AppTheme.textSecondary,
                                ),
                              ),
                            ],
                          ),
                          GestureDetector(
                            onTap: () => Navigator.push(
                                context,
                                MaterialPageRoute<void>(
                                    builder: (_) =>
                                        const PasswordRecoveryScreen())),
                            child: Text(
                              'Forgot Password?',
                              style: AppTheme.labelMd.copyWith(
                                decoration: TextDecoration.underline,
                              ),
                            ),
                          ),
                        ],
                      ),

                    const SizedBox(height: AppTheme.spaceMd),

                    // ── SIGN IN BUTTON ──
                    SizedBox(
                      width: double.infinity,
                      height: 48,
                      child: ElevatedButton(
                        onPressed: _isLoading ? null : _submit,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: AppTheme.primary,
                          foregroundColor: AppTheme.onPrimary,
                          shape: RoundedRectangleBorder(
                            borderRadius:
                                BorderRadius.circular(AppTheme.radiusXl),
                          ),
                          elevation: 0,
                        ),
                        child: _isLoading
                            ? const SizedBox(
                                width: 24,
                                height: 24,
                                child: CircularProgressIndicator(
                                  color: Colors.white,
                                  strokeWidth: 2.5,
                                ),
                              )
                            : Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  Icon(
                                    _isRegisterMode
                                        ? Icons.person_add
                                        : Icons.lock,
                                    size: 18,
                                  ),
                                  const SizedBox(width: 8),
                                  Text(
                                    _isRegisterMode
                                        ? 'Create Account'
                                        : 'Sign In',
                                    style: AppTheme.labelLg.copyWith(
                                      color: AppTheme.onPrimary,
                                    ),
                                  ),
                                ],
                              ),
                      ),
                    ),

                    const SizedBox(height: AppTheme.spaceMd),
                  ],
                ),
              ),
            ),

            // ── FOOTER ──
            Container(
              padding: const EdgeInsets.symmetric(
                  horizontal: AppTheme.margin, vertical: AppTheme.spaceSm),
              decoration: const BoxDecoration(
                color: AppTheme.surface,
                border: Border(
                  top: BorderSide(color: AppTheme.borderSubtle),
                ),
              ),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(
                        _isRegisterMode
                            ? 'Already have an account? '
                            : "Don't have an account? ",
                        style: AppTheme.bodyMd.copyWith(
                          color: AppTheme.textSecondary,
                        ),
                      ),
                      GestureDetector(
                        onTap: () =>
                            setState(() => _isRegisterMode = !_isRegisterMode),
                        child: Text(
                          _isRegisterMode ? 'Sign In' : 'Create an Account',
                          style: AppTheme.labelLg,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: AppTheme.spaceSm),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.verified_user,
                          size: 15, color: AppTheme.textTertiary),
                      const SizedBox(width: 6),
                      Text(
                        'PCI-DSS Level 1 & Encrypted Authentication',
                        style: AppTheme.labelSm.copyWith(
                          color: AppTheme.textTertiary,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
