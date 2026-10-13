import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme.dart';
import '../../data/models/achievement_item.dart';
import '../providers/gamification_providers.dart';

class AchievementsScreen extends ConsumerWidget {
  const AchievementsScreen({super.key});

  IconData _resolveIcon(String iconName) {
    switch (iconName.toLowerCase()) {
      case 'check_circle':
        return Icons.check_circle_rounded;
      case 'timer':
        return Icons.timer_rounded;
      case 'repeat':
        return Icons.repeat_rounded;
      case 'hourglass_full':
        return Icons.hourglass_full_rounded;
      case 'scuba_diving':
        return Icons.pool_rounded;
      case 'local_fire_department':
        return Icons.local_fire_department_rounded;
      case 'whatshot':
        return Icons.whatshot_rounded;
      case 'task_alt':
        return Icons.task_alt_rounded;
      default:
        return Icons.emoji_events_rounded;
    }
  }

  String _formatDate(DateTime? date) {
    if (date == null) return '';
    final local = date.toLocal();
    final months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
    ];
    final month = months[local.month - 1];
    return '$month ${local.day}, ${local.year}';
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final achievementsAsync = ref.watch(achievementsProvider);

    return Scaffold(
      backgroundColor: AppTheme.midnightBase,
      appBar: AppBar(
        title: const Text('Achievements'),
        backgroundColor: Colors.transparent,
        elevation: 0,
      ),
      body: achievementsAsync.when(
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
                  'Failed to load achievements',
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
                  onPressed: () => ref.invalidate(achievementsProvider),
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
        data: (achievements) {
          if (achievements.isEmpty) {
            return const Center(
              child: Text(
                'No achievements available',
                style: TextStyle(color: Colors.white54, fontSize: 16),
              ),
            );
          }

          final unlockedCount = achievements.where((a) => a.isUnlocked).length;
          final totalCount = achievements.length;
          final progressRatio = totalCount > 0 ? unlockedCount / totalCount : 0.0;

          return RefreshIndicator(
            onRefresh: () => ref.refresh(achievementsProvider.future),
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(20.0),
              children: [
                _buildSummaryBanner(context, unlockedCount, totalCount, progressRatio),
                const SizedBox(height: 20),
                ...achievements.map((item) => _buildAchievementCard(context, item)),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _buildSummaryBanner(
    BuildContext context,
    int unlockedCount,
    int totalCount,
    double progressRatio,
  ) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [
            AppTheme.focusPurple.withValues(alpha: 0.3),
            AppTheme.growthTeal.withValues(alpha: 0.15),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppTheme.focusPurple.withValues(alpha: 0.3)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  const Icon(Icons.emoji_events_rounded, color: AppTheme.streakAmber, size: 28),
                  const SizedBox(width: 10),
                  Text(
                    'Unlocked $unlockedCount of $totalCount',
                    style: const TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      color: Colors.white,
                    ),
                  ),
                ],
              ),
              Text(
                '${(progressRatio * 100).toInt()}%',
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w600,
                  color: AppTheme.growthTeal,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: progressRatio,
              minHeight: 8,
              backgroundColor: Colors.white.withValues(alpha: 0.1),
              valueColor: const AlwaysStoppedAnimation<Color>(AppTheme.growthTeal),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildAchievementCard(BuildContext context, AchievementItem item) {
    final isUnlocked = item.isUnlocked;
    final iconData = _resolveIcon(item.iconName);

    return Container(
      margin: const EdgeInsets.only(bottom: 14),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: isUnlocked
            ? Colors.white.withValues(alpha: 0.07)
            : Colors.white.withValues(alpha: 0.02),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: isUnlocked
              ? AppTheme.streakAmber.withValues(alpha: 0.4)
              : Colors.white.withValues(alpha: 0.05),
          width: isUnlocked ? 1.5 : 1.0,
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: isUnlocked
                  ? AppTheme.streakAmber.withValues(alpha: 0.2)
                  : Colors.white.withValues(alpha: 0.05),
            ),
            child: Icon(
              iconData,
              size: 28,
              color: isUnlocked ? AppTheme.streakAmber : Colors.white30,
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Expanded(
                      child: Text(
                        item.title,
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                          color: isUnlocked ? Colors.white : Colors.white60,
                        ),
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                      decoration: BoxDecoration(
                        color: isUnlocked
                            ? AppTheme.growthTeal.withValues(alpha: 0.2)
                            : Colors.white.withValues(alpha: 0.05),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Text(
                        '+${item.xpReward} XP',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: isUnlocked ? AppTheme.growthTeal : Colors.white38,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  item.description,
                  style: TextStyle(
                    fontSize: 13,
                    color: isUnlocked ? Colors.white70 : Colors.white38,
                  ),
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Icon(
                      isUnlocked ? Icons.check_circle_rounded : Icons.lock_outline_rounded,
                      size: 14,
                      color: isUnlocked ? AppTheme.growthTeal : Colors.white30,
                    ),
                    const SizedBox(width: 4),
                    Text(
                      isUnlocked
                          ? (item.unlockedAt != null
                              ? 'Unlocked on ${_formatDate(item.unlockedAt)}'
                              : 'Unlocked')
                          : 'Locked',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w500,
                        color: isUnlocked ? AppTheme.growthTeal : Colors.white30,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
