import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../auth/presentation/screens/login_screen.dart';
import '../../data/models/user_profile.dart';
import 'settings_screen.dart';

class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  Future<void> _logout(BuildContext context, WidgetRef ref) async {
    final repo = ref.read(authRepositoryProvider);
    await repo.logout();
    ref.read(authStateProvider.notifier).state = false;
    if (context.mounted) {
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const LoginScreen()),
        (route) => false,
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profileAsync = ref.watch(userProfileProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile'),
        backgroundColor: Colors.transparent,
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.settings_rounded),
            onPressed: () {
              Navigator.push(context, MaterialPageRoute(builder: (_) => const SettingsScreen()));
            },
          )
        ],
      ),
      body: profileAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppTheme.focusPurple),
        ),
        error: (error, stack) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24.0),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline_rounded, size: 48, color: Colors.redAccent),
                const SizedBox(height: 16),
                Text(
                  'Failed to load profile',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(color: Colors.white),
                ),
                const SizedBox(height: 8),
                Text(
                  error.toString(),
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Colors.white54, fontSize: 13),
                ),
                const SizedBox(height: 16),
                ElevatedButton.icon(
                  onPressed: () => ref.refresh(userProfileProvider),
                  icon: const Icon(Icons.refresh_rounded),
                  label: const Text('Retry'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppTheme.focusPurple,
                  ),
                ),
              ],
            ),
          ),
        ),
        data: (profile) => RefreshIndicator(
          onRefresh: () => ref.refresh(userProfileProvider.future),
          child: SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(24.0),
            child: Column(
              children: [
                const CircleAvatar(
                  radius: 50,
                  backgroundColor: AppTheme.focusPurple,
                  child: Icon(Icons.person_rounded, size: 50, color: Colors.white),
                ),
                const SizedBox(height: 16),
                Text(
                  profile.displayName.isNotEmpty ? profile.displayName : 'User',
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 4),
                Text(
                  profile.email,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: Colors.white54),
                ),
                const SizedBox(height: 8),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  decoration: BoxDecoration(
                    color: AppTheme.focusPurple.withOpacity(0.2),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(
                    'Level ${profile.level} • ${profile.xp} XP',
                    style: const TextStyle(color: AppTheme.focusPurple, fontWeight: FontWeight.w600, fontSize: 13),
                  ),
                ),
                const SizedBox(height: 32),
                _buildStatCards(profile),
                const SizedBox(height: 40),
                SizedBox(
                  width: double.infinity,
                  child: OutlinedButton.icon(
                    onPressed: () => _logout(context, ref),
                    icon: const Icon(Icons.logout_rounded, color: Colors.redAccent),
                    label: const Text('Log Out', style: TextStyle(color: Colors.redAccent)),
                    style: OutlinedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      side: const BorderSide(color: Colors.redAccent),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(16),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  String _formatFocusHours(int totalMinutes) {
    if (totalMinutes <= 0) return '0';
    if (totalMinutes % 60 == 0) {
      return (totalMinutes ~/ 60).toString();
    }
    final hours = totalMinutes / 60.0;
    return hours.toStringAsFixed(1);
  }

  Widget _buildStatCards(UserProfile profile) {
    return Row(
      children: [
        Expanded(
          child: _StatCard(
            title: 'Current Streak',
            value: '${profile.currentStreak}',
            unit: 'Days',
            icon: Icons.local_fire_department_rounded,
            color: AppTheme.streakAmber,
          ),
        ),
        const SizedBox(width: 16),
        Expanded(
          child: _StatCard(
            title: 'Total Focus',
            value: _formatFocusHours(profile.totalFocusMinutes),
            unit: 'Hours',
            icon: Icons.timer_rounded,
            color: AppTheme.growthTeal,
          ),
        ),
      ],
    );
  }
}

class _StatCard extends StatelessWidget {
  final String title;
  final String value;
  final String unit;
  final IconData icon;
  final Color color;

  const _StatCard({
    required this.title,
    required this.value,
    required this.unit,
    required this.icon,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white.withOpacity(0.05),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Colors.white.withOpacity(0.05)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: color, size: 28),
          const SizedBox(height: 12),
          Text(
            title,
            style: const TextStyle(color: Colors.white70, fontSize: 12),
          ),
          const SizedBox(height: 4),
          Row(
            crossAxisAlignment: CrossAxisAlignment.baseline,
            textBaseline: TextBaseline.alphabetic,
            children: [
              Text(
                value,
                style: const TextStyle(
                  fontSize: 24,
                  fontWeight: FontWeight.bold,
                  color: Colors.white,
                ),
              ),
              const SizedBox(width: 4),
              Text(
                unit,
                style: const TextStyle(
                  fontSize: 12,
                  color: Colors.white54,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
