import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../auth/presentation/screens/login_screen.dart';
import '../../../gamification/data/models/gamification_summary.dart';
import '../../../gamification/data/models/xp_transaction_item.dart';
import '../../../gamification/presentation/providers/gamification_providers.dart';
import '../../../gamification/presentation/screens/achievements_screen.dart';
import '../../data/models/user_profile.dart';
import 'settings_screen.dart';
import '../../../subjects/presentation/screens/subjects_screen.dart';
import '../../../daily_goals/presentation/screens/daily_goals_screen.dart';

class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  Future<void> _logout(BuildContext context, WidgetRef ref) async {
    final repo = ref.read(authRepositoryProvider);
    await repo.logout();
    ref.read(authStateProvider.notifier).state = false;
    ref.invalidate(userProfileProvider);
    ref.invalidate(gamificationSummaryProvider);
    ref.invalidate(achievementsProvider);
    ref.invalidate(xpHistoryProvider);
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
    final summaryAsync = ref.watch(gamificationSummaryProvider);
    final historyAsync = ref.watch(xpHistoryProvider);

    return Scaffold(
      backgroundColor: AppTheme.midnightBase,
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
                  onPressed: () {
                    ref.invalidate(userProfileProvider);
                    ref.invalidate(gamificationSummaryProvider);
                    ref.invalidate(xpHistoryProvider);
                  },
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
          onRefresh: () async {
            ref.invalidate(userProfileProvider);
            ref.invalidate(gamificationSummaryProvider);
            ref.invalidate(achievementsProvider);
            ref.invalidate(xpHistoryProvider);
            try {
              await ref.read(userProfileProvider.future);
            } catch (_) {}
          },
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
                const SizedBox(height: 24),

                // 1. Authoritative Level Progress Card
                _buildLevelProgressCard(context, profile, summaryAsync),
                const SizedBox(height: 20),

                // 2. Quick Stat Cards (Streak & Total Focus)
                _buildStatCards(profile),
                const SizedBox(height: 20),

                // 3. Achievements Navigation Card
                _buildAchievementsCard(context, summaryAsync),
                const SizedBox(height: 20),

                // 4. Daily Goals & Subjects Navigation Cards
                _buildStudyFeaturesCards(context),
                const SizedBox(height: 24),

                // 5. Recent XP History Section
                _buildRecentXpSection(context, historyAsync),
                const SizedBox(height: 36),

                // 6. Logout Button
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

  Widget _buildLevelProgressCard(
    BuildContext context,
    UserProfile profile,
    AsyncValue<GamificationSummary> summaryAsync,
  ) {
    final int level = summaryAsync.valueOrNull?.level ?? profile.level;
    final int totalXp = summaryAsync.valueOrNull?.totalXp ?? profile.xp;
    final double fraction = (summaryAsync.valueOrNull?.progressFraction ?? 0.0).clamp(0.0, 1.0);
    final double percentage = summaryAsync.valueOrNull?.progressPercentage ?? (fraction * 100.0);
    final int earnedInLevel = summaryAsync.valueOrNull?.xpEarnedInCurrentLevel ?? 0;
    final int requiredForNext = summaryAsync.valueOrNull?.xpRequiredForNextLevel ?? 100;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [
            AppTheme.focusPurple.withValues(alpha: 0.35),
            AppTheme.growthTeal.withValues(alpha: 0.12),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppTheme.focusPurple.withValues(alpha: 0.35)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppTheme.focusPurple,
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Text(
                      'Level $level',
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 14,
                      ),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Text(
                    '$totalXp XP',
                    style: const TextStyle(
                      color: Colors.white70,
                      fontWeight: FontWeight.w600,
                      fontSize: 14,
                    ),
                  ),
                ],
              ),
              Text(
                '${percentage.toStringAsFixed(0)}%',
                style: const TextStyle(
                  color: AppTheme.growthTeal,
                  fontWeight: FontWeight.bold,
                  fontSize: 15,
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: fraction,
              minHeight: 10,
              backgroundColor: Colors.white.withValues(alpha: 0.1),
              valueColor: const AlwaysStoppedAnimation<Color>(AppTheme.growthTeal),
            ),
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                '$earnedInLevel / $requiredForNext XP',
                style: const TextStyle(color: Colors.white60, fontSize: 12),
              ),
              Text(
                'Next: Level ${level + 1}',
                style: const TextStyle(color: Colors.white38, fontSize: 12),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildAchievementsCard(
    BuildContext context,
    AsyncValue<GamificationSummary> summaryAsync,
  ) {
    final int unlocked = summaryAsync.valueOrNull?.unlockedAchievements ?? 0;
    final int total = summaryAsync.valueOrNull?.totalAchievements ?? 8;

    return InkWell(
      onTap: () {
        Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => const AchievementsScreen()),
        );
      },
      borderRadius: BorderRadius.circular(20),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 16),
        decoration: BoxDecoration(
          color: Colors.white.withValues(alpha: 0.05),
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: Colors.white.withValues(alpha: 0.06)),
        ),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: AppTheme.streakAmber.withValues(alpha: 0.2),
                shape: BoxShape.circle,
              ),
              child: const Icon(Icons.emoji_events_rounded, color: AppTheme.streakAmber, size: 24),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Achievements',
                    style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 15),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    '$unlocked of $total Unlocked',
                    style: const TextStyle(color: Colors.white54, fontSize: 12),
                  ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right_rounded, color: Colors.white54),
          ],
        ),
      ),
    );
  }

  Widget _buildStudyFeaturesCards(BuildContext context) {
    return Column(
      children: [
        InkWell(
          key: const Key('dailyGoalNavCard'),
          onTap: () {
            Navigator.push(
              context,
              MaterialPageRoute(builder: (_) => const DailyGoalsScreen()),
            );
          },
          borderRadius: BorderRadius.circular(20),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 16),
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.05),
              borderRadius: BorderRadius.circular(20),
              border: Border.all(color: Colors.white.withValues(alpha: 0.06)),
            ),
            child: Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: const Color(0xFF00D9C0).withValues(alpha: 0.2),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.flag_rounded, color: Color(0xFF00D9C0), size: 24),
                ),
                const SizedBox(width: 14),
                const Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Daily Study Goal',
                        style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white),
                      ),
                      SizedBox(height: 3),
                      Text(
                        'Configure and track your daily focus target',
                        style: TextStyle(fontSize: 12, color: Colors.white54),
                      ),
                    ],
                  ),
                ),
                const Icon(Icons.chevron_right_rounded, color: Colors.white38),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        InkWell(
          key: const Key('subjectsNavCard'),
          onTap: () {
            Navigator.push(
              context,
              MaterialPageRoute(builder: (_) => const SubjectsScreen()),
            );
          },
          borderRadius: BorderRadius.circular(20),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 16),
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.05),
              borderRadius: BorderRadius.circular(20),
              border: Border.all(color: Colors.white.withValues(alpha: 0.06)),
            ),
            child: Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: const Color(0xFF6C5CE7).withValues(alpha: 0.2),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.auto_stories_rounded, color: Color(0xFFA29BFE), size: 24),
                ),
                const SizedBox(width: 14),
                const Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Manage Subjects',
                        style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white),
                      ),
                      SizedBox(height: 3),
                      Text(
                        'Organize sessions and tasks by subject',
                        style: TextStyle(fontSize: 12, color: Colors.white54),
                      ),
                    ],
                  ),
                ),
                const Icon(Icons.chevron_right_rounded, color: Colors.white38),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildRecentXpSection(
    BuildContext context,
    AsyncValue<List<XpTransactionItem>> historyAsync,
  ) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Row(
          children: [
            Icon(Icons.history_rounded, size: 20, color: Colors.white70),
            SizedBox(width: 8),
            Text(
              'Recent XP Activity',
              style: TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.bold,
                fontSize: 16,
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        historyAsync.when(
          loading: () => const Center(
            child: Padding(
              padding: EdgeInsets.all(16.0),
              child: CircularProgressIndicator(color: AppTheme.focusPurple),
            ),
          ),
          error: (error, _) => Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.03),
              borderRadius: BorderRadius.circular(16),
            ),
            child: const Center(
              child: Text(
                'Unable to load XP activity',
                style: TextStyle(color: Colors.white38, fontSize: 13),
              ),
            ),
          ),
          data: (history) {
            if (history.isEmpty) {
              return Container(
                width: double.infinity,
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.03),
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: Colors.white.withValues(alpha: 0.04)),
                ),
                child: const Center(
                  child: Text(
                    'No XP activity recorded yet',
                    style: TextStyle(color: Colors.white38, fontSize: 13),
                  ),
                ),
              );
            }

            return Column(
              children: history.map((item) => _buildXpItem(item)).toList(),
            );
          },
        ),
      ],
    );
  }

  Widget _buildXpItem(XpTransactionItem item) {
    IconData icon;
    Color iconColor;

    switch (item.sourceType.toUpperCase()) {
      case 'TASK':
        icon = Icons.check_circle_outline_rounded;
        iconColor = AppTheme.growthTeal;
        break;
      case 'HABIT':
        icon = Icons.repeat_rounded;
        iconColor = AppTheme.focusPurple;
        break;
      case 'STUDYSESSION':
        icon = Icons.timer_rounded;
        iconColor = AppTheme.streakAmber;
        break;
      case 'ACHIEVEMENT':
        icon = Icons.emoji_events_rounded;
        iconColor = AppTheme.streakAmber;
        break;
      default:
        icon = Icons.stars_rounded;
        iconColor = Colors.white54;
        break;
    }

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.04),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: Colors.white.withValues(alpha: 0.04)),
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: iconColor.withValues(alpha: 0.15),
              shape: BoxShape.circle,
            ),
            child: Icon(icon, color: iconColor, size: 18),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.description.isNotEmpty ? item.description : item.sourceType,
                  style: const TextStyle(color: Colors.white, fontSize: 13, fontWeight: FontWeight.w500),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 2),
                Text(
                  _formatTimestamp(item.createdAt),
                  style: const TextStyle(color: Colors.white38, fontSize: 11),
                ),
              ],
            ),
          ),
          const SizedBox(width: 10),
          Text(
            '+${item.amount} XP',
            style: const TextStyle(
              color: AppTheme.growthTeal,
              fontWeight: FontWeight.bold,
              fontSize: 14,
            ),
          ),
        ],
      ),
    );
  }

  String _formatTimestamp(DateTime dateTime) {
    final now = DateTime.now();
    final diff = now.difference(dateTime.toLocal());
    if (diff.inMinutes < 1) return 'Just now';
    if (diff.inMinutes < 60) return '${diff.inMinutes}m ago';
    if (diff.inHours < 24) return '${diff.inHours}h ago';
    if (diff.inDays < 7) return '${diff.inDays}d ago';
    final local = dateTime.toLocal();
    return '${local.month}/${local.day}/${local.year}';
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
        color: Colors.white.withValues(alpha: 0.05),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Colors.white.withValues(alpha: 0.05)),
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
