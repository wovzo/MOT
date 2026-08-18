import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../shared/widgets/custom_text_field.dart';
import '../../../../shared/widgets/primary_button.dart';
import '../providers/auth_provider.dart';

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _isLoading = false;

  Future<void> _register() async {
    setState(() => _isLoading = true);
    try {
      final repo = ref.read(authRepositoryProvider);
      await repo.register(_emailController.text, _passwordController.text, _nameController.text);
      ref.read(authStateProvider.notifier).state = true;
      // Navigate to Home or Onboarding
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString())));
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(backgroundColor: Colors.transparent, elevation: 0),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('Create Account', style: Theme.of(context).textTheme.headlineMedium),
              const SizedBox(height: 32),
              CustomTextField(label: 'Display Name', controller: _nameController),
              const SizedBox(height: 16),
              CustomTextField(label: 'Email', controller: _emailController),
              const SizedBox(height: 16),
              CustomTextField(label: 'Password', controller: _passwordController, obscureText: true),
              const SizedBox(height: 32),
              PrimaryButton(text: 'Sign Up', onPressed: _register, isLoading: _isLoading),
            ],
          ),
        ),
      ),
    );
  }
}
