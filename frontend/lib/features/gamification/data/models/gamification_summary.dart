class GamificationSummary {
  final int level;
  final int totalXp;
  final int currentLevelThresholdXp;
  final int nextLevelThresholdXp;
  final int xpEarnedInCurrentLevel;
  final int xpRequiredForNextLevel;
  final double progressFraction;
  final double progressPercentage;
  final int totalAchievements;
  final int unlockedAchievements;

  GamificationSummary({
    required this.level,
    required this.totalXp,
    required this.currentLevelThresholdXp,
    required this.nextLevelThresholdXp,
    required this.xpEarnedInCurrentLevel,
    required this.xpRequiredForNextLevel,
    required this.progressFraction,
    required this.progressPercentage,
    required this.totalAchievements,
    required this.unlockedAchievements,
  });

  factory GamificationSummary.fromJson(Map<String, dynamic> json) {
    int parseInt(dynamic val, [int fallback = 0]) {
      if (val is num) return val.toInt();
      if (val is String) return int.tryParse(val) ?? fallback;
      return fallback;
    }

    double parseDouble(dynamic val, [double fallback = 0.0]) {
      if (val is num) return val.toDouble();
      if (val is String) return double.tryParse(val) ?? fallback;
      return fallback;
    }

    final rawFraction = parseDouble(json['progressFraction'] ?? json['ProgressFraction'], 0.0);
    final clampedFraction = rawFraction.clamp(0.0, 1.0);

    return GamificationSummary(
      level: parseInt(json['level'] ?? json['Level'], 1),
      totalXp: parseInt(json['totalXp'] ?? json['TotalXp'], 0),
      currentLevelThresholdXp: parseInt(json['currentLevelThresholdXp'] ?? json['CurrentLevelThresholdXp'], 0),
      nextLevelThresholdXp: parseInt(json['nextLevelThresholdXp'] ?? json['NextLevelThresholdXp'], 100),
      xpEarnedInCurrentLevel: parseInt(json['xpEarnedInCurrentLevel'] ?? json['XpEarnedInCurrentLevel'], 0),
      xpRequiredForNextLevel: parseInt(json['xpRequiredForNextLevel'] ?? json['XpRequiredForNextLevel'], 100),
      progressFraction: clampedFraction,
      progressPercentage: parseDouble(json['progressPercentage'] ?? json['ProgressPercentage'], clampedFraction * 100.0),
      totalAchievements: parseInt(json['totalAchievements'] ?? json['TotalAchievements'], 8),
      unlockedAchievements: parseInt(json['unlockedAchievements'] ?? json['UnlockedAchievements'], 0),
    );
  }
}
