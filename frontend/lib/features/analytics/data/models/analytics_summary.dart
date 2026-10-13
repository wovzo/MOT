class FocusAnalyticsSummary {
  final int totalFocusMinutes;
  final int todayFocusMinutes;
  final int thisWeekFocusMinutes;
  final int totalSessionsCount;
  final int completedSessionsCount;
  final int incompleteSessionsCount;
  final double averageSessionMinutes;
  final TaskAnalyticsSummary tasks;
  final HabitAnalyticsSummary habits;

  FocusAnalyticsSummary({
    required this.totalFocusMinutes,
    required this.todayFocusMinutes,
    required this.thisWeekFocusMinutes,
    required this.totalSessionsCount,
    required this.completedSessionsCount,
    required this.incompleteSessionsCount,
    required this.averageSessionMinutes,
    required this.tasks,
    required this.habits,
  });

  factory FocusAnalyticsSummary.fromJson(Map<String, dynamic> json) {
    return FocusAnalyticsSummary(
      totalFocusMinutes: (json['totalFocusMinutes'] as num?)?.toInt() ?? 0,
      todayFocusMinutes: (json['todayFocusMinutes'] as num?)?.toInt() ?? 0,
      thisWeekFocusMinutes: (json['thisWeekFocusMinutes'] as num?)?.toInt() ?? 0,
      totalSessionsCount: (json['totalSessionsCount'] as num?)?.toInt() ?? 0,
      completedSessionsCount: (json['completedSessionsCount'] as num?)?.toInt() ?? 0,
      incompleteSessionsCount: (json['incompleteSessionsCount'] as num?)?.toInt() ?? 0,
      averageSessionMinutes: (json['averageSessionMinutes'] as num?)?.toDouble() ?? 0.0,
      tasks: json['tasks'] is Map<String, dynamic>
          ? TaskAnalyticsSummary.fromJson(json['tasks'] as Map<String, dynamic>)
          : json['tasks'] is Map
              ? TaskAnalyticsSummary.fromJson(Map<String, dynamic>.from(json['tasks']))
              : TaskAnalyticsSummary.empty(),
      habits: json['habits'] is Map<String, dynamic>
          ? HabitAnalyticsSummary.fromJson(json['habits'] as Map<String, dynamic>)
          : json['habits'] is Map
              ? HabitAnalyticsSummary.fromJson(Map<String, dynamic>.from(json['habits']))
              : HabitAnalyticsSummary.empty(),
    );
  }
}

class TaskAnalyticsSummary {
  final int totalTasks;
  final int completedTasks;
  final int pendingTasks;
  final double completionRatePercentage;

  TaskAnalyticsSummary({
    required this.totalTasks,
    required this.completedTasks,
    required this.pendingTasks,
    required this.completionRatePercentage,
  });

  factory TaskAnalyticsSummary.empty() => TaskAnalyticsSummary(
        totalTasks: 0,
        completedTasks: 0,
        pendingTasks: 0,
        completionRatePercentage: 0.0,
      );

  factory TaskAnalyticsSummary.fromJson(Map<String, dynamic> json) {
    return TaskAnalyticsSummary(
      totalTasks: (json['totalTasks'] as num?)?.toInt() ?? 0,
      completedTasks: (json['completedTasks'] as num?)?.toInt() ?? 0,
      pendingTasks: (json['pendingTasks'] as num?)?.toInt() ?? 0,
      completionRatePercentage: (json['completionRatePercentage'] as num?)?.toDouble() ?? 0.0,
    );
  }
}

class HabitAnalyticsSummary {
  final int totalHabits;
  final int totalCompletions;
  final int completionsToday;
  final int completionsThisWeek;
  final int activeStreaksCount;

  HabitAnalyticsSummary({
    required this.totalHabits,
    required this.totalCompletions,
    required this.completionsToday,
    required this.completionsThisWeek,
    required this.activeStreaksCount,
  });

  factory HabitAnalyticsSummary.empty() => HabitAnalyticsSummary(
        totalHabits: 0,
        totalCompletions: 0,
        completionsToday: 0,
        completionsThisWeek: 0,
        activeStreaksCount: 0,
      );

  factory HabitAnalyticsSummary.fromJson(Map<String, dynamic> json) {
    return HabitAnalyticsSummary(
      totalHabits: (json['totalHabits'] as num?)?.toInt() ?? 0,
      totalCompletions: (json['totalCompletions'] as num?)?.toInt() ?? 0,
      completionsToday: (json['completionsToday'] as num?)?.toInt() ?? 0,
      completionsThisWeek: (json['completionsThisWeek'] as num?)?.toInt() ?? 0,
      activeStreaksCount: (json['activeStreaksCount'] as num?)?.toInt() ?? 0,
    );
  }
}
