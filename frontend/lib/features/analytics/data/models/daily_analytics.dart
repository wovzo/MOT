class DailyAnalytics {
  final String startDate;
  final String endDate;
  final int totalFocusMinutes;
  final int totalCompletedSessions;
  final int totalHabitCompletions;
  final List<DailyDataPoint> dailyBreakdown;

  DailyAnalytics({
    required this.startDate,
    required this.endDate,
    required this.totalFocusMinutes,
    required this.totalCompletedSessions,
    required this.totalHabitCompletions,
    required this.dailyBreakdown,
  });

  factory DailyAnalytics.fromJson(Map<String, dynamic> json) {
    var rawList = json['dailyBreakdown'];
    List<DailyDataPoint> points = [];
    if (rawList is List) {
      points = rawList
          .map((item) => item is Map<String, dynamic>
              ? DailyDataPoint.fromJson(item)
              : item is Map
                  ? DailyDataPoint.fromJson(Map<String, dynamic>.from(item))
                  : DailyDataPoint(date: '', focusMinutes: 0, completedSessionsCount: 0, habitCompletionsCount: 0))
          .toList();
    }

    return DailyAnalytics(
      startDate: (json['startDate'] ?? '').toString(),
      endDate: (json['endDate'] ?? '').toString(),
      totalFocusMinutes: (json['totalFocusMinutes'] as num?)?.toInt() ?? 0,
      totalCompletedSessions: (json['totalCompletedSessions'] as num?)?.toInt() ?? 0,
      totalHabitCompletions: (json['totalHabitCompletions'] as num?)?.toInt() ?? 0,
      dailyBreakdown: points,
    );
  }
}

class DailyDataPoint {
  final String date;
  final int focusMinutes;
  final int completedSessionsCount;
  final int habitCompletionsCount;

  DailyDataPoint({
    required this.date,
    required this.focusMinutes,
    required this.completedSessionsCount,
    required this.habitCompletionsCount,
  });

  factory DailyDataPoint.fromJson(Map<String, dynamic> json) {
    return DailyDataPoint(
      date: (json['date'] ?? '').toString(),
      focusMinutes: (json['focusMinutes'] as num?)?.toInt() ?? 0,
      completedSessionsCount: (json['completedSessionsCount'] as num?)?.toInt() ?? 0,
      habitCompletionsCount: (json['habitCompletionsCount'] as num?)?.toInt() ?? 0,
    );
  }
}
