import '../../../daily_goals/data/models/daily_goal_progress_model.dart';
import '../../../gamification/data/models/gamification_summary.dart';
import '../../../habits/data/models/habit_model.dart';
import '../../../subjects/data/models/subject_model.dart';
import '../../../tasks/data/models/daily_task.dart';
import '../../../timer/data/models/study_session_model.dart';

class DashboardSummary {
  final DailyGoalProgress dailyGoal;
  final StudySession? activeSession;
  final DashboardTaskSummary tasks;
  final DashboardHabitSummary habits;
  final GamificationSummary gamification;
  final DashboardStreakSummary streak;
  final List<Subject> subjects;
  final List<StudySession> recentSessions;
  final List<DashboardDayActivity> weeklyActivity;

  DashboardSummary({
    required this.dailyGoal,
    this.activeSession,
    required this.tasks,
    required this.habits,
    required this.gamification,
    required this.streak,
    required this.subjects,
    required this.recentSessions,
    required this.weeklyActivity,
  });

  factory DashboardSummary.fromJson(Map<String, dynamic> json) {
    return DashboardSummary(
      dailyGoal: json['dailyGoal'] != null
          ? DailyGoalProgress.fromJson(json['dailyGoal'] as Map<String, dynamic>)
          : DailyGoalProgress(date: ''),
      activeSession: json['activeSession'] != null
          ? StudySession.fromJson(json['activeSession'] as Map<String, dynamic>)
          : null,
      tasks: json['tasks'] != null
          ? DashboardTaskSummary.fromJson(json['tasks'] as Map<String, dynamic>)
          : DashboardTaskSummary.empty(),
      habits: json['habits'] != null
          ? DashboardHabitSummary.fromJson(json['habits'] as Map<String, dynamic>)
          : DashboardHabitSummary.empty(),
      gamification: json['gamification'] != null
          ? GamificationSummary.fromJson(json['gamification'] as Map<String, dynamic>)
          : GamificationSummary(
              level: 1,
              totalXp: 0,
              currentLevelThresholdXp: 0,
              nextLevelThresholdXp: 100,
              xpEarnedInCurrentLevel: 0,
              xpRequiredForNextLevel: 100,
              progressFraction: 0.0,
              progressPercentage: 0.0,
              totalAchievements: 8,
              unlockedAchievements: 0,
            ),
      streak: json['streak'] != null
          ? DashboardStreakSummary.fromJson(json['streak'] as Map<String, dynamic>)
          : DashboardStreakSummary.empty(),
      subjects: json['subjects'] is List
          ? (json['subjects'] as List).map((s) => Subject.fromJson(s as Map<String, dynamic>)).toList()
          : [],
      recentSessions: json['recentSessions'] is List
          ? (json['recentSessions'] as List).map((s) => StudySession.fromJson(s as Map<String, dynamic>)).toList()
          : [],
      weeklyActivity: json['weeklyActivity'] is List
          ? (json['weeklyActivity'] as List).map((w) => DashboardDayActivity.fromJson(w as Map<String, dynamic>)).toList()
          : [],
    );
  }
}

class DashboardTaskSummary {
  final int totalCount;
  final int completedCount;
  final int pendingCount;
  final double completionPercentage;
  final List<DailyTask> topPendingTasks;

  DashboardTaskSummary({
    required this.totalCount,
    required this.completedCount,
    required this.pendingCount,
    required this.completionPercentage,
    required this.topPendingTasks,
  });

  factory DashboardTaskSummary.empty() => DashboardTaskSummary(
        totalCount: 0,
        completedCount: 0,
        pendingCount: 0,
        completionPercentage: 0.0,
        topPendingTasks: [],
      );

  factory DashboardTaskSummary.fromJson(Map<String, dynamic> json) {
    final total = json['totalCount'] is num ? (json['totalCount'] as num).toInt() : 0;
    final completed = json['completedCount'] is num ? (json['completedCount'] as num).toInt() : 0;
    final pending = json['pendingCount'] is num ? (json['pendingCount'] as num).toInt() : 0;
    final percentage = json['completionPercentage'] is num
        ? (json['completionPercentage'] as num).toDouble()
        : 0.0;

    final topTasks = json['topPendingTasks'] is List
        ? (json['topPendingTasks'] as List).map((t) => DailyTask.fromJson(t as Map<String, dynamic>)).toList()
        : <DailyTask>[];

    return DashboardTaskSummary(
      totalCount: total,
      completedCount: completed,
      pendingCount: pending,
      completionPercentage: percentage,
      topPendingTasks: topTasks,
    );
  }
}

class DashboardHabitSummary {
  final int totalCount;
  final int completedTodayCount;
  final double completionPercentage;
  final List<Habit> habits;

  DashboardHabitSummary({
    required this.totalCount,
    required this.completedTodayCount,
    required this.completionPercentage,
    required this.habits,
  });

  factory DashboardHabitSummary.empty() => DashboardHabitSummary(
        totalCount: 0,
        completedTodayCount: 0,
        completionPercentage: 0.0,
        habits: [],
      );

  factory DashboardHabitSummary.fromJson(Map<String, dynamic> json) {
    final total = json['totalCount'] is num ? (json['totalCount'] as num).toInt() : 0;
    final completedToday = json['completedTodayCount'] is num ? (json['completedTodayCount'] as num).toInt() : 0;
    final percentage = json['completionPercentage'] is num
        ? (json['completionPercentage'] as num).toDouble()
        : 0.0;

    final habitsList = json['habits'] is List
        ? (json['habits'] as List).map((h) => Habit.fromJson(h as Map<String, dynamic>)).toList()
        : <Habit>[];

    return DashboardHabitSummary(
      totalCount: total,
      completedTodayCount: completedToday,
      completionPercentage: percentage,
      habits: habitsList,
    );
  }
}

class DashboardStreakSummary {
  final int currentStreak;
  final bool isActiveToday;
  final int longestStreak;

  DashboardStreakSummary({
    required this.currentStreak,
    required this.isActiveToday,
    required this.longestStreak,
  });

  factory DashboardStreakSummary.empty() => DashboardStreakSummary(
        currentStreak: 0,
        isActiveToday: false,
        longestStreak: 0,
      );

  factory DashboardStreakSummary.fromJson(Map<String, dynamic> json) {
    return DashboardStreakSummary(
      currentStreak: json['currentStreak'] is num ? (json['currentStreak'] as num).toInt() : 0,
      isActiveToday: json['isActiveToday'] == true,
      longestStreak: json['longestStreak'] is num ? (json['longestStreak'] as num).toInt() : 0,
    );
  }
}

class DashboardDayActivity {
  final String date;
  final String dayLabel;
  final int focusMinutes;
  final int completedSessionsCount;

  DashboardDayActivity({
    required this.date,
    required this.dayLabel,
    required this.focusMinutes,
    required this.completedSessionsCount,
  });

  factory DashboardDayActivity.fromJson(Map<String, dynamic> json) {
    return DashboardDayActivity(
      date: json['date']?.toString() ?? '',
      dayLabel: json['dayLabel']?.toString() ?? '',
      focusMinutes: json['focusMinutes'] is num ? (json['focusMinutes'] as num).toInt() : 0,
      completedSessionsCount: json['completedSessionsCount'] is num
          ? (json['completedSessionsCount'] as num).toInt()
          : 0,
    );
  }
}
