import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme.dart';
import '../../../classroom/presentation/screens/classroom_join_screen.dart';
import '../../../daily_goals/presentation/screens/daily_goals_screen.dart';
import '../../../gamification/presentation/screens/achievements_screen.dart';
import '../../../habits/presentation/screens/habit_dashboard_screen.dart';
import '../../../subjects/presentation/screens/subjects_screen.dart';
import '../../../tasks/presentation/screens/task_dashboard_screen.dart';
import '../../../timer/presentation/screens/timer_screen.dart';
import '../../../timer/data/models/study_session_model.dart';
import '../../data/models/dashboard_summary_model.dart';
import '../providers/dashboard_provider.dart';

class DashboardScreen extends ConsumerWidget {
  final VoidCallback? onNavigateToTasks;
  final VoidCallback? onNavigateToHabits;
  final VoidCallback? onNavigateToTimer;

  const DashboardScreen({
    super.key,
    this.onNavigateToTasks,
    this.onNavigateToHabits,
    this.onNavigateToTimer,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final summaryAsync = ref.watch(dashboardSummaryProvider);

    return Scaffold(
      backgroundColor: AppTheme.midnightBase,
      appBar: AppBar(
        title: const Text(
          'Mind On Track',
          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 20),
        ),
        backgroundColor: Colors.transparent,
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.video_camera_front_outlined, color: AppTheme.growthTeal),
            tooltip: 'Live Study Room',
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute(builder: (_) => const ClassroomJoinScreen()),
              );
            },
          ),
          IconButton(
            icon: const Icon(Icons.refresh_rounded, color: Colors.white70),
            tooltip: 'Refresh',
            onPressed: () => ref.refresh(dashboardSummaryProvider.future),
          ),
        ],
      ),
      body: summaryAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppTheme.focusPurple),
        ),
        error: (error, stack) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24.0),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline, size: 48, color: Colors.redAccent),
                const SizedBox(height: 16),
                Text(
                  'Failed to load dashboard',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(color: Colors.white),
                ),
                const SizedBox(height: 8),
                Text(
                  error.toString(),
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Colors.white60, fontSize: 13),
                ),
                const SizedBox(height: 20),
                ElevatedButton.icon(
                  onPressed: () => ref.refresh(dashboardSummaryProvider.future),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Try Again'),
                )
              ],
            ),
          ),
        ),
        data: (summary) {
          return RefreshIndicator(
            color: AppTheme.focusPurple,
            onRefresh: () async => ref.refresh(dashboardSummaryProvider.future),
            child: ListView(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              children: [
                // 1. Greeting & Gamification Bar
                _buildHeader(context, summary),
                const SizedBox(height: 16),

                // 2. Active Focus Session Banner (if running or paused)
                if (summary.activeSession != null) ...[
                  _buildActiveSessionBanner(context, summary.activeSession!),
                  const SizedBox(height: 16),
                ],

                // 3. Daily Study Goal Card
                _buildDailyGoalCard(context, summary.dailyGoal),
                const SizedBox(height: 16),

                // 4. Weekly Activity Summary (7 Days)
                _buildWeeklyActivityCard(context, summary.weeklyActivity),
                const SizedBox(height: 16),

                // 5. Tasks and Habits Progress Grid
                _buildTasksAndHabitsRow(context, summary),
                const SizedBox(height: 16),

                // 6. Subjects Overview
                _buildSubjectsSection(context, summary.subjects),
                const SizedBox(height: 16),

                // 7. Recent Study Sessions
                _buildRecentSessionsSection(context, summary.recentSessions),
                const SizedBox(height: 24),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _buildHeader(BuildContext context, DashboardSummary summary) {
    final streak = summary.streak;
    final game = summary.gamification;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFF1E1B3A),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Welcome back,',
                    style: TextStyle(color: Colors.white60, fontSize: 13),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    'Scholar',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: Colors.white,
                          fontWeight: FontWeight.bold,
                        ),
                  ),
                ],
              ),

              // Streak Badge
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                decoration: BoxDecoration(
                  color: streak.isActiveToday
                      ? AppTheme.streakAmber.withValues(alpha: 0.2)
                      : Colors.white.withValues(alpha: 0.06),
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(
                    color: streak.isActiveToday
                        ? AppTheme.streakAmber.withValues(alpha: 0.6)
                        : Colors.white.withValues(alpha: 0.1),
                  ),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(
                      Icons.local_fire_department_rounded,
                      color: streak.isActiveToday ? AppTheme.streakAmber : Colors.white54,
                      size: 20,
                    ),
                    const SizedBox(width: 4),
                    Text(
                      '${streak.currentStreak} ${streak.currentStreak == 1 ? "Day" : "Days"}',
                      style: TextStyle(
                        color: streak.isActiveToday ? AppTheme.streakAmber : Colors.white70,
                        fontWeight: FontWeight.bold,
                        fontSize: 13,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),

          // Gamification Level and XP progress
          InkWell(
            onTap: () {
              Navigator.push(
                context,
                MaterialPageRoute(builder: (_) => const AchievementsScreen()),
              );
            },
            borderRadius: BorderRadius.circular(12),
            child: Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.04),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(6),
                    decoration: BoxDecoration(
                      color: AppTheme.focusPurple.withValues(alpha: 0.2),
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(Icons.military_tech_rounded, color: AppTheme.focusPurple, size: 20),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text(
                              'Level ${game.level}',
                              style: const TextStyle(
                                color: Colors.white,
                                fontWeight: FontWeight.bold,
                                fontSize: 13,
                              ),
                            ),
                            Text(
                              '${game.totalXp} XP',
                              style: const TextStyle(
                                color: AppTheme.growthTeal,
                                fontWeight: FontWeight.w600,
                                fontSize: 12,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 6),
                        ClipRRect(
                          borderRadius: BorderRadius.circular(4),
                          child: LinearProgressIndicator(
                            value: game.progressFraction.clamp(0.0, 1.0),
                            backgroundColor: Colors.white.withValues(alpha: 0.1),
                            valueColor: const AlwaysStoppedAnimation<Color>(AppTheme.focusPurple),
                            minHeight: 5,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 8),
                  const Icon(Icons.chevron_right, color: Colors.white38, size: 18),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildActiveSessionBanner(BuildContext context, StudySession session) {
    final statusText = session.isPaused ? 'PAUSED' : 'IN PROGRESS';
    final statusColor = session.isPaused ? AppTheme.streakAmber : AppTheme.growthTeal;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppTheme.focusPurple.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppTheme.focusPurple.withValues(alpha: 0.5)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 8,
                height: 8,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: statusColor,
                ),
              ),
              const SizedBox(width: 8),
              Text(
                'ACTIVE STUDY SESSION • $statusText',
                style: TextStyle(
                  color: statusColor,
                  fontWeight: FontWeight.bold,
                  fontSize: 11,
                  letterSpacing: 0.5,
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      session.title,
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Elapsed: ${session.durationMinutes} min (${session.elapsedSeconds}s)',
                      style: const TextStyle(color: Colors.white70, fontSize: 13),
                    ),
                  ],
                ),
              ),
              ElevatedButton.icon(
                onPressed: () {
                  if (onNavigateToTimer != null) {
                    onNavigateToTimer!();
                  } else {
                    Navigator.push(
                      context,
                      MaterialPageRoute(builder: (_) => const TimerScreen()),
                    );
                  }
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppTheme.focusPurple,
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                ),
                icon: const Icon(Icons.timer, size: 16),
                label: const Text('Open Timer', style: TextStyle(fontSize: 13)),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildDailyGoalCard(BuildContext context, dynamic goal) {
    final actual = goal.actualMinutes as int;
    final target = goal.targetMinutes as int;
    final remaining = goal.remainingMinutes as int;
    final percentage = (goal.progressPercentage as double).clamp(0.0, 100.0);
    final isAchieved = goal.isAchieved as bool;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFF1E1B3A),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
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
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: AppTheme.growthTeal.withValues(alpha: 0.15),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Icon(Icons.track_changes_rounded, color: AppTheme.growthTeal, size: 20),
                  ),
                  const SizedBox(width: 10),
                  const Text(
                    'Daily Study Goal',
                    style: TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.bold,
                      fontSize: 16,
                    ),
                  ),
                ],
              ),
              InkWell(
                onTap: () {
                  Navigator.push(
                    context,
                    MaterialPageRoute(builder: (_) => const DailyGoalsScreen()),
                  );
                },
                borderRadius: BorderRadius.circular(8),
                child: const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  child: Row(
                    children: [
                      Text('Configure', style: TextStyle(color: AppTheme.focusPurple, fontSize: 13)),
                      SizedBox(width: 2),
                      Icon(Icons.chevron_right, size: 16, color: AppTheme.focusPurple),
                    ],
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              // Circular progress indicator
              SizedBox(
                width: 76,
                height: 76,
                child: Stack(
                  alignment: Alignment.center,
                  children: [
                    CircularProgressIndicator(
                      value: percentage / 100.0,
                      backgroundColor: Colors.white.withValues(alpha: 0.08),
                      valueColor: AlwaysStoppedAnimation<Color>(
                        isAchieved ? AppTheme.growthTeal : AppTheme.focusPurple,
                      ),
                      strokeWidth: 8,
                    ),
                    Text(
                      '${percentage.toStringAsFixed(0)}%',
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 14,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 20),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '$actual / $target mins',
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 20,
                      ),
                    ),
                    const SizedBox(height: 4),
                    if (isAchieved)
                      Row(
                        children: const [
                          Icon(Icons.check_circle_rounded, color: AppTheme.growthTeal, size: 16),
                          SizedBox(width: 4),
                          Text(
                            'Goal completed today! 🎉',
                            style: TextStyle(color: AppTheme.growthTeal, fontSize: 13, fontWeight: FontWeight.w600),
                          ),
                        ],
                      )
                    else
                      Text(
                        '$remaining mins remaining today',
                        style: const TextStyle(color: Colors.white60, fontSize: 13),
                      ),
                    const SizedBox(height: 8),
                    Text(
                      '${goal.completedSessionsCount} sessions completed today',
                      style: const TextStyle(color: Colors.white38, fontSize: 11),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildWeeklyActivityCard(BuildContext context, List<DashboardDayActivity> activity) {
    final maxMinutes = activity.map((a) => a.focusMinutes).fold<int>(60, (max, m) => m > max ? m : max);
    final totalWeekMinutes = activity.fold<int>(0, (sum, a) => sum + a.focusMinutes);

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFF1E1B3A),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Weekly Focus Activity',
                style: TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.bold,
                  fontSize: 16,
                ),
              ),
              Text(
                '$totalWeekMinutes mins total',
                style: const TextStyle(color: AppTheme.growthTeal, fontWeight: FontWeight.w600, fontSize: 13),
              ),
            ],
          ),
          const SizedBox(height: 18),

          // 7-Day bar graph
          SizedBox(
            height: 110,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceAround,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: activity.map((day) {
                final barHeight = maxMinutes > 0
                    ? ((day.focusMinutes / maxMinutes) * 60).clamp(4.0, 60.0)
                    : 4.0;
                final isToday = day == activity.last;

                return Column(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    Text(
                      day.focusMinutes > 0 ? '${day.focusMinutes}m' : '',
                      style: const TextStyle(color: Colors.white54, fontSize: 10),
                    ),
                    const SizedBox(height: 4),
                    Container(
                      width: 18,
                      height: barHeight,
                      decoration: BoxDecoration(
                        color: day.focusMinutes > 0
                            ? (isToday ? AppTheme.growthTeal : AppTheme.focusPurple)
                            : Colors.white.withValues(alpha: 0.08),
                        borderRadius: BorderRadius.circular(6),
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      day.dayLabel,
                      style: TextStyle(
                        color: isToday ? Colors.white : Colors.white60,
                        fontWeight: isToday ? FontWeight.bold : FontWeight.normal,
                        fontSize: 11,
                      ),
                    ),
                  ],
                );
              }).toList(),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTasksAndHabitsRow(BuildContext context, DashboardSummary summary) {
    return Row(
      children: [
        // Tasks Card
        Expanded(
          child: InkWell(
            onTap: () {
              if (onNavigateToTasks != null) {
                onNavigateToTasks!();
              } else {
                Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const TaskDashboardScreen()),
                );
              }
            },
            borderRadius: BorderRadius.circular(20),
            child: Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: const Color(0xFF1E1B3A),
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Container(
                        padding: const EdgeInsets.all(6),
                        decoration: BoxDecoration(
                          color: AppTheme.growthTeal.withValues(alpha: 0.15),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(Icons.check_circle_outline, color: AppTheme.growthTeal, size: 18),
                      ),
                      const Icon(Icons.chevron_right, color: Colors.white38, size: 18),
                    ],
                  ),
                  const SizedBox(height: 12),
                  const Text('Daily Tasks', style: TextStyle(color: Colors.white70, fontSize: 12)),
                  const SizedBox(height: 4),
                  Text(
                    '${summary.tasks.completedCount}/${summary.tasks.totalCount}',
                    style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 18),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    '${summary.tasks.completionPercentage.toStringAsFixed(0)}% done',
                    style: const TextStyle(color: AppTheme.growthTeal, fontSize: 11, fontWeight: FontWeight.w600),
                  ),
                ],
              ),
            ),
          ),
        ),
        const SizedBox(width: 12),

        // Habits Card
        Expanded(
          child: InkWell(
            onTap: () {
              if (onNavigateToHabits != null) {
                onNavigateToHabits!();
              } else {
                Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const HabitDashboardScreen()),
                );
              }
            },
            borderRadius: BorderRadius.circular(20),
            child: Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: const Color(0xFF1E1B3A),
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Container(
                        padding: const EdgeInsets.all(6),
                        decoration: BoxDecoration(
                          color: AppTheme.focusPurple.withValues(alpha: 0.2),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(Icons.repeat_rounded, color: AppTheme.focusPurple, size: 18),
                      ),
                      const Icon(Icons.chevron_right, color: Colors.white38, size: 18),
                    ],
                  ),
                  const SizedBox(height: 12),
                  const Text('Habits Today', style: TextStyle(color: Colors.white70, fontSize: 12)),
                  const SizedBox(height: 4),
                  Text(
                    '${summary.habits.completedTodayCount}/${summary.habits.totalCount}',
                    style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 18),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    '${summary.habits.completionPercentage.toStringAsFixed(0)}% completed',
                    style: const TextStyle(color: AppTheme.focusPurple, fontSize: 11, fontWeight: FontWeight.w600),
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildSubjectsSection(BuildContext context, dynamic subjects) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFF1E1B3A),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Subjects',
                style: TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.bold,
                  fontSize: 16,
                ),
              ),
              InkWell(
                onTap: () {
                  Navigator.push(
                    context,
                    MaterialPageRoute(builder: (_) => const SubjectsScreen()),
                  );
                },
                borderRadius: BorderRadius.circular(8),
                child: const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  child: Row(
                    children: [
                      Text('Manage', style: TextStyle(color: AppTheme.focusPurple, fontSize: 13)),
                      SizedBox(width: 2),
                      Icon(Icons.chevron_right, size: 16, color: AppTheme.focusPurple),
                    ],
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (subjects.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text(
                'No subjects created yet. Add subjects to categorize your study time.',
                style: TextStyle(color: Colors.white54, fontSize: 13),
              ),
            )
          else
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: subjects.map<Widget>((subject) {
                Color chipColor = AppTheme.focusPurple;
                if (subject.colorHex != null && subject.colorHex!.isNotEmpty) {
                  try {
                    chipColor = Color(int.parse(subject.colorHex!.replaceFirst('#', '0xFF')));
                  } catch (_) {}
                }

                return Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  decoration: BoxDecoration(
                    color: chipColor.withValues(alpha: 0.15),
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: chipColor.withValues(alpha: 0.4)),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Container(
                        width: 8,
                        height: 8,
                        decoration: BoxDecoration(shape: BoxShape.circle, color: chipColor),
                      ),
                      const SizedBox(width: 6),
                      Text(
                        subject.name,
                        style: TextStyle(
                          color: chipColor,
                          fontWeight: FontWeight.w600,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                );
              }).toList(),
            ),
        ],
      ),
    );
  }

  Widget _buildRecentSessionsSection(BuildContext context, dynamic recentSessions) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFF1E1B3A),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Recent Study Sessions',
                style: TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.bold,
                  fontSize: 16,
                ),
              ),
              InkWell(
                onTap: () {
                  if (onNavigateToTimer != null) {
                    onNavigateToTimer!();
                  } else {
                    Navigator.push(
                      context,
                      MaterialPageRoute(builder: (_) => const TimerScreen()),
                    );
                  }
                },
                borderRadius: BorderRadius.circular(8),
                child: const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  child: Row(
                    children: [
                      Text('Timer', style: TextStyle(color: AppTheme.focusPurple, fontSize: 13)),
                      SizedBox(width: 2),
                      Icon(Icons.chevron_right, size: 16, color: AppTheme.focusPurple),
                    ],
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (recentSessions.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text(
                'No completed study sessions yet. Start your first session in the Timer tab!',
                style: TextStyle(color: Colors.white54, fontSize: 13),
              ),
            )
          else
            ListView.separated(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              itemCount: recentSessions.length,
              separatorBuilder: (_, __) => Divider(color: Colors.white.withValues(alpha: 0.06), height: 16),
              itemBuilder: (context, index) {
                final session = recentSessions[index];
                return Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.all(8),
                      decoration: BoxDecoration(
                        color: AppTheme.focusPurple.withValues(alpha: 0.15),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: const Icon(Icons.timer_outlined, color: AppTheme.focusPurple, size: 18),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            session.title,
                            style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 14),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            '${session.durationMinutes} minutes focused',
                            style: const TextStyle(color: Colors.white60, fontSize: 12),
                          ),
                        ],
                      ),
                    ),
                    const Icon(Icons.check_circle_outline, color: AppTheme.growthTeal, size: 18),
                  ],
                );
              },
            ),
        ],
      ),
    );
  }
}
