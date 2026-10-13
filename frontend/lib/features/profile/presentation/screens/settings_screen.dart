import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../auth/presentation/screens/login_screen.dart';
import '../../../gamification/presentation/providers/gamification_providers.dart';

class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});

  void _showDeleteAccountDialog(BuildContext context, WidgetRef ref) {
    final passwordController = TextEditingController();
    bool isLoading = false;
    String? errorMessage;

    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setState) => AlertDialog(
          backgroundColor: const Color(0xFF1E1A38),
          title: const Text('Delete Account', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'This action is permanent and cannot be undone. All your study sessions, tasks, and habits will be permanently removed.',
                style: TextStyle(color: Colors.white70, fontSize: 13),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: passwordController,
                obscureText: true,
                style: const TextStyle(color: Colors.white),
                decoration: InputDecoration(
                  labelText: 'Confirm Password',
                  labelStyle: const TextStyle(color: Colors.white54),
                  errorText: errorMessage,
                  enabledBorder: OutlineInputBorder(
                    borderSide: const BorderSide(color: Colors.white24),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderSide: const BorderSide(color: Colors.redAccent),
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
              ),
              if (isLoading) ...[
                const SizedBox(height: 16),
                const Center(child: CircularProgressIndicator(color: Colors.redAccent)),
              ],
            ],
          ),
          actions: [
            TextButton(
              onPressed: isLoading ? null : () => Navigator.of(dialogContext).pop(),
              child: const Text('Cancel', style: TextStyle(color: Colors.white54)),
            ),
            ElevatedButton(
              style: ElevatedButton.styleFrom(backgroundColor: Colors.redAccent),
              onPressed: isLoading
                  ? null
                  : () async {
                      final password = passwordController.text;
                      if (password.isEmpty) {
                        setState(() => errorMessage = 'Password is required.');
                        return;
                      }

                      setState(() {
                        isLoading = true;
                        errorMessage = null;
                      });

                      try {
                        final authRepo = ref.read(authRepositoryProvider);
                        await authRepo.deleteAccount(password);

                        // Success cleanup: invalidate profile and gamification caches and reset auth state
                        ref.invalidate(userProfileProvider);
                        ref.invalidate(gamificationSummaryProvider);
                        ref.invalidate(achievementsProvider);
                        ref.invalidate(xpHistoryProvider);
                        ref.read(authStateProvider.notifier).state = false;

                        if (dialogContext.mounted) {
                          Navigator.of(dialogContext).pop();
                        }

                        if (context.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(content: Text('Account deleted successfully.')),
                          );
                          Navigator.of(context).pushAndRemoveUntil(
                            MaterialPageRoute(builder: (_) => const LoginScreen()),
                            (route) => false,
                          );
                        }
                      } catch (e) {
                        setState(() {
                          isLoading = false;
                          errorMessage = e.toString().replaceAll('Exception: ', '');
                        });
                      }
                    },
              child: const Text('Delete Permanently', style: TextStyle(color: Colors.white)),
            ),
          ],
        ),
      ),
    );
  }

  void _showAccountOptions(BuildContext context, WidgetRef ref) {
    showModalBottomSheet(
      context: context,
      backgroundColor: const Color(0xFF1E1A38),
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (sheetContext) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: Colors.white24,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
              const SizedBox(height: 16),
              const Text(
                'Account Settings',
                style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 16),
              ListTile(
                leading: const Icon(Icons.delete_forever, color: Colors.redAccent),
                title: const Text('Delete Account', style: TextStyle(color: Colors.redAccent, fontWeight: FontWeight.w600)),
                subtitle: const Text('Permanently remove your account and all data', style: TextStyle(color: Colors.white54, fontSize: 12)),
                onTap: () {
                  Navigator.of(sheetContext).pop();
                  _showDeleteAccountDialog(context, ref);
                },
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      backgroundColor: const Color(0xFF121026),
      appBar: AppBar(
        title: const Text('Settings', style: TextStyle(color: Colors.white)),
        backgroundColor: Colors.transparent,
        elevation: 0,
        iconTheme: const IconThemeData(color: Colors.white),
      ),
      body: ListView(
        children: [
          ListTile(
            leading: const Icon(Icons.person, color: Colors.white70),
            title: const Text('Account', style: TextStyle(color: Colors.white)),
            subtitle: const Text('Manage account and security', style: TextStyle(color: Colors.white38, fontSize: 12)),
            trailing: const Icon(Icons.chevron_right, color: Colors.white54),
            onTap: () => _showAccountOptions(context, ref),
          ),
          ListTile(
            leading: const Icon(Icons.notifications, color: Colors.white70),
            title: const Text('Notifications', style: TextStyle(color: Colors.white)),
            trailing: const Icon(Icons.chevron_right, color: Colors.white54),
            onTap: () {
              ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Notification settings coming soon!')));
            },
          ),
          ListTile(
            leading: const Icon(Icons.color_lens, color: Colors.white70),
            title: const Text('Theme', style: TextStyle(color: Colors.white)),
            trailing: const Icon(Icons.chevron_right, color: Colors.white54),
            onTap: () {
              ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Theme settings coming soon!')));
            },
          ),
        ],
      ),
    );
  }
}
