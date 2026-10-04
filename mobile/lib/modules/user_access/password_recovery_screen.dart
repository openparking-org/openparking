import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import '../../services/api_config.dart';
import '../../services/api_response.dart';

class PasswordRecoveryScreen extends StatefulWidget {
  const PasswordRecoveryScreen({super.key});
  @override
  State<PasswordRecoveryScreen> createState() => _PasswordRecoveryScreenState();
}

class _PasswordRecoveryScreenState extends State<PasswordRecoveryScreen> {
  final email = TextEditingController(),
      token = TextEditingController(),
      password = TextEditingController();
  final form = GlobalKey<FormState>();
  bool busy = false, sent = false;
  String? message;
  @override
  void dispose() {
    email.dispose();
    token.dispose();
    password.dispose();
    super.dispose();
  }

  Future<void> submit() async {
    if (!form.currentState!.validate()) return;
    setState(() {
      busy = true;
      message = null;
    });
    try {
      final response = await http
          .post(
              Uri.parse(
                  '${ApiConfig.baseUrl}/api/users/${sent ? 'reset-password' : 'forgot-password'}'),
              headers: {'Content-Type': 'application/json'},
              body: jsonEncode(sent
                  ? {'token': token.text.trim(), 'password': password.text}
                  : {'email': email.text.trim()}))
          .timeout(const Duration(seconds: 20));
      final data = responseData(response) as Map<String, dynamic>;
      if (!mounted) return;
      if (sent) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(data['message'])));
        Navigator.pop(context);
        return;
      }
      setState(() {
        sent = true;
        message = data['message'];
      });
    } catch (e) {
      if (mounted) {
        setState(() => message = e.toString().replaceFirst('Exception: ', ''));
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
      appBar: AppBar(title: const Text('Reset password')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Form(
            key: form,
            child: Column(children: [
              TextFormField(
                  controller: email,
                  enabled: !busy && !sent,
                  keyboardType: TextInputType.emailAddress,
                  decoration: const InputDecoration(labelText: 'Email'),
                  validator: (value) => value == null || !value.contains('@')
                      ? 'Enter your account email.'
                      : null),
              if (sent) ...[
                const SizedBox(height: 16),
                TextFormField(
                    controller: token,
                    decoration: const InputDecoration(
                        labelText: 'Reset code from email'),
                    validator: (value) => value == null || value.trim().isEmpty
                        ? 'Paste the code from your email.'
                        : null),
                const SizedBox(height: 16),
                TextFormField(
                    controller: password,
                    obscureText: true,
                    decoration:
                        const InputDecoration(labelText: 'New password'),
                    validator: (value) => value == null || value.length < 8
                        ? 'Use at least 8 characters.'
                        : null),
              ],
              if (message != null)
                Padding(
                    padding: const EdgeInsets.symmetric(vertical: 16),
                    child: Text(message!)),
              const SizedBox(height: 16),
              FilledButton(
                  onPressed: busy ? null : submit,
                  child: Text(busy
                      ? 'Please wait…'
                      : sent
                          ? 'Change password'
                          : 'Send reset email')),
              if (sent)
                TextButton(
                    onPressed: busy
                        ? null
                        : () => setState(() {
                              sent = false;
                              token.clear();
                            }),
                    child: const Text('Request another email')),
            ])),
      ));
}
