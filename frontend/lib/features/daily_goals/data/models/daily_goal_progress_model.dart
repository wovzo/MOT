class DailyGoalProgress {
  final String date;
  final int targetMinutes;
  final int actualMinutes;
  final int remainingMinutes;
  final double progressPercentage;
  final bool isAchieved;
  final bool isCustomTarget;
  final int completedSessionsCount;

  DailyGoalProgress({
    required this.date,
    this.targetMinutes = 60,
    this.actualMinutes = 0,
    this.remainingMinutes = 60,
    this.progressPercentage = 0.0,
    this.isAchieved = false,
    this.isCustomTarget = false,
    this.completedSessionsCount = 0,
  });

  factory DailyGoalProgress.fromJson(Map<String, dynamic> json) {
    final target = json['targetMinutes'] is num ? (json['targetMinutes'] as num).toInt() : 60;
    final actual = json['actualMinutes'] is num ? (json['actualMinutes'] as num).toInt() : 0;
    final remaining = json['remainingMinutes'] is num
        ? (json['remainingMinutes'] as num).toInt()
        : (target > actual ? target - actual : 0);
    final percentage = json['progressPercentage'] is num
        ? (json['progressPercentage'] as num).toDouble()
        : (target > 0 ? (actual / target * 100).clamp(0.0, 100.0) : 100.0);

    return DailyGoalProgress(
      date: json['date']?.toString() ?? '',
      targetMinutes: target,
      actualMinutes: actual,
      remainingMinutes: remaining,
      progressPercentage: percentage,
      isAchieved: json['isAchieved'] == true,
      isCustomTarget: json['isCustomTarget'] == true,
      completedSessionsCount: json['completedSessionsCount'] is num
          ? (json['completedSessionsCount'] as num).toInt()
          : 0,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'date': date,
      'targetMinutes': targetMinutes,
      'actualMinutes': actualMinutes,
      'remainingMinutes': remainingMinutes,
      'progressPercentage': progressPercentage,
      'isAchieved': isAchieved,
      'isCustomTarget': isCustomTarget,
      'completedSessionsCount': completedSessionsCount,
    };
  }
}
