import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/auth_provider.dart';
import '../../core/theme.dart';
import '../../services/user_service.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _emailController =
      TextEditingController(text: 'driver@openparking.test');
  final _passwordController = TextEditingController(text: 'Password123!');
  bool _isLoading = false;
  bool _obscurePassword = true;

  // Registration state
  bool _isRegisterMode = false;
  final _nameController = TextEditingController();

  // Tab state
  bool _isPhoneTab = true;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _nameController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final email = _emailController.text.trim();
    final password = _passwordController.text.trim();

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

      final success =
          await ref.read(authProvider.notifier).login(email, password);

      if (!mounted) return;
      setState(() => _isLoading = false);

      if (!success) {
        _showSnackBar('Authentication failed. Check credentials.',
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
                    onPressed: () {},
                    padding: EdgeInsets.zero,
                    constraints: const BoxConstraints(
                        minWidth: 40, minHeight: 40),
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
                    constraints: const BoxConstraints(
                        minWidth: 40, minHeight: 40),
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
                          : 'Sign in to access your digital parking passes, fleet, and automated payments.',
                      style: AppTheme.bodyMd
                          .copyWith(color: AppTheme.textSecondary),
                    ),

                    const SizedBox(height: AppTheme.spaceLg),

                    // Tab Switcher
                    if (!_isRegisterMode)
                      Container(
                        padding: const EdgeInsets.all(4),
                        decoration: BoxDecoration(
                          color: AppTheme.surfaceSubtle,
                          borderRadius:
                              BorderRadius.circular(AppTheme.radiusXl),
                          border: Border.all(color: AppTheme.borderSubtle),
                        ),
                        child: Row(
                          children: [
                            Expanded(
                              child: GestureDetector(
                                onTap: () =>
                                    setState(() => _isPhoneTab = true),
                                child: AnimatedContainer(
                                  duration:
                                      const Duration(milliseconds: 150),
                                  padding:
                                      const EdgeInsets.symmetric(vertical: 10),
                                  decoration: BoxDecoration(
                                    color: _isPhoneTab
                                        ? AppTheme.surfacePure
                                        : Colors.transparent,
                                    borderRadius: BorderRadius.circular(
                                        AppTheme.radiusLg),
                                    boxShadow: _isPhoneTab
                                        ? [
                                            BoxShadow(
                                              color: Colors.black
                                                  .withValues(alpha: 0.04),
                                              blurRadius: 4,
                                              offset:
                                                  const Offset(0, 1),
                                            ),
                                          ]
                                        : null,
                                  ),
                                  alignment: Alignment.center,
                                  child: Text(
                                    'Phone Number',
                                    style: AppTheme.labelMd.copyWith(
                                      color: _isPhoneTab
                                          ? AppTheme.textPrimary
                                          : AppTheme.textSecondary,
                                    ),
                                  ),
                                ),
                              ),
                            ),
                            Expanded(
                              child: GestureDetector(
                                onTap: () =>
                                    setState(() => _isPhoneTab = false),
                                child: AnimatedContainer(
                                  duration:
                                      const Duration(milliseconds: 150),
                                  padding:
                                      const EdgeInsets.symmetric(vertical: 10),
                                  decoration: BoxDecoration(
                                    color: !_isPhoneTab
                                        ? AppTheme.surfacePure
                                        : Colors.transparent,
                                    borderRadius: BorderRadius.circular(
                                        AppTheme.radiusLg),
                                    boxShadow: !_isPhoneTab
                                        ? [
                                            BoxShadow(
                                              color: Colors.black
                                                  .withValues(alpha: 0.04),
                                              blurRadius: 4,
                                              offset:
                                                  const Offset(0, 1),
                                            ),
                                          ]
                                        : null,
                                  ),
                                  alignment: Alignment.center,
                                  child: Text(
                                    'Email',
                                    style: AppTheme.labelMd.copyWith(
                                      color: !_isPhoneTab
                                          ? AppTheme.textPrimary
                                          : AppTheme.textSecondary,
                                    ),
                                  ),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),

                    const SizedBox(height: AppTheme.spaceLg),

                    // ── NAME FIELD (Register mode only) ──
                    if (_isRegisterMode) ...[
                      Text('Full Name', style: AppTheme.labelMd),
                      const SizedBox(height: 6),
                      TextField(
                        controller: _nameController,
                        style: AppTheme.bodyMd,
                        decoration: InputDecoration(
                          hintText: 'John Doe',
                          prefixIcon: const Icon(Icons.person_outline,
                              color: AppTheme.textTertiary, size: 18),
                        ),
                      ),
                      const SizedBox(height: AppTheme.spaceMd),
                    ],

                    // ── EMAIL FIELD ──
                    Text(
                      _isPhoneTab && !_isRegisterMode
                          ? 'Mobile Phone'
                          : 'Email Address',
                      style: AppTheme.labelMd,
                    ),
                    const SizedBox(height: 6),
                    TextField(
                      controller: _emailController,
                      keyboardType: _isPhoneTab && !_isRegisterMode
                          ? TextInputType.phone
                          : TextInputType.emailAddress,
                      style: AppTheme.bodyMd,
                      decoration: InputDecoration(
                        hintText: _isPhoneTab && !_isRegisterMode
                            ? '(555) 000-0000'
                            : 'name@domain.com',
                        prefixIcon: Icon(
                          _isPhoneTab && !_isRegisterMode
                              ? Icons.phone_outlined
                              : Icons.mail_outline,
                          color: AppTheme.textTertiary,
                          size: 18,
                        ),
                      ),
                    ),
                    const SizedBox(height: AppTheme.spaceMd),

                    // ── PASSWORD FIELD ──
                    Text('Password', style: AppTheme.labelMd),
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
                                  value: true,
                                  onChanged: (v) {},
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
                            onTap: () {},
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
                            borderRadius: BorderRadius.circular(
                                AppTheme.radiusXl),
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

                    const SizedBox(height: AppTheme.spaceLg),

                    // ── BIOMETRICS BUTTON ──
                    if (!_isRegisterMode)
                      Container(
                        decoration: BoxDecoration(
                          color: AppTheme.surfaceSubtle,
                          borderRadius:
                              BorderRadius.circular(AppTheme.radiusXl),
                          border: Border.all(color: AppTheme.borderSubtle),
                        ),
                        child: Material(
                          color: Colors.transparent,
                          child: InkWell(
                            borderRadius:
                                BorderRadius.circular(AppTheme.radiusXl),
                            onTap: () {},
                            child: Padding(
                              padding: const EdgeInsets.symmetric(
                                  horizontal: 16, vertical: 12),
                              child: Row(
                                children: [
                                  Container(
                                    width: 32,
                                    height: 32,
                                    decoration: BoxDecoration(
                                      color: AppTheme.surfacePure,
                                      borderRadius: BorderRadius.circular(8),
                                      border: Border.all(
                                          color: AppTheme.borderSubtle),
                                    ),
                                    child: const Icon(Icons.fingerprint,
                                        size: 20,
                                        color: AppTheme.textPrimary),
                                  ),
                                  const SizedBox(width: 12),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          'Sign in with Biometrics',
                                          style: AppTheme.labelMd.copyWith(
                                            color: AppTheme.textPrimary,
                                          ),
                                        ),
                                        Text(
                                          'Touch ID or Face ID',
                                          style: AppTheme.bodySm.copyWith(
                                            color: AppTheme.textTertiary,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                  const Icon(Icons.arrow_forward,
                                      size: 18,
                                      color: AppTheme.textSecondary),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ),

                    const SizedBox(height: AppTheme.spaceLg),

                    // ── DIVIDER ──
                    if (!_isRegisterMode)
                      Row(
                        children: [
                          const Expanded(
                            child: Divider(color: AppTheme.borderSubtle),
                          ),
                          Padding(
                            padding: const EdgeInsets.symmetric(
                                horizontal: 12),
                            child: Text(
                              'OR CONTINUE WITH',
                              style: AppTheme.labelSm.copyWith(
                                color: AppTheme.textTertiary,
                                letterSpacing: 0.8,
                              ),
                            ),
                          ),
                          const Expanded(
                            child: Divider(color: AppTheme.borderSubtle),
                          ),
                        ],
                      ),

                    const SizedBox(height: AppTheme.spaceMd),

                    // ── SOCIAL BUTTONS ──
                    if (!_isRegisterMode)
                      Row(
                        children: [
                          Expanded(
                            child: _SocialButton(
                              icon: Icons.apple,
                              label: 'Apple',
                              onTap: () {},
                            ),
                          ),
                          const SizedBox(width: AppTheme.spaceSm),
                          Expanded(
                            child: _SocialButton(
                              icon: Icons.g_mobiledata,
                              label: 'Google',
                              onTap: () {},
                            ),
                          ),
                        ],
                      ),

                    const SizedBox(height: AppTheme.spaceMd),

                    // Quick test accounts
                    Center(
                      child: Text(
                        'Quick Test Accounts',
                        style: AppTheme.labelSm.copyWith(
                          color: AppTheme.textTertiary,
                        ),
                      ),
                    ),
                    const SizedBox(height: 8),
                    Center(
                      child: ActionChip(
                        avatar: const Icon(Icons.flash_on,
                            size: 14, color: AppTheme.primary),
                        label: const Text('Driver'),
                        backgroundColor: AppTheme.surfaceSubtle,
                        labelStyle: AppTheme.bodySm.copyWith(
                          color: AppTheme.textSecondary,
                        ),
                        side: const BorderSide(color: AppTheme.borderSubtle),
                        onPressed: () {
                          setState(() {
                            _isRegisterMode = false;
                            _emailController.text =
                                'driver@openparking.test';
                            _passwordController.text = 'Password123!';
                          });
                        },
                      ),
                    ),
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

class _SocialButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  const _SocialButton({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 46,
      child: OutlinedButton(
        onPressed: onTap,
        style: OutlinedButton.styleFrom(
          backgroundColor: AppTheme.surfacePure,
          foregroundColor: AppTheme.textPrimary,
          side: const BorderSide(color: AppTheme.borderSubtle),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppTheme.radiusXl),
          ),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 18, color: AppTheme.textPrimary),
            const SizedBox(width: 8),
            Text(
              label,
              style: AppTheme.labelMd.copyWith(color: AppTheme.textPrimary),
            ),
          ],
        ),
      ),
    );
  }
}
