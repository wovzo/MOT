class AchievementItem {
  final String code;
  final String title;
  final String description;
  final String iconName;
  final int xpReward;
  final bool isUnlocked;
  final DateTime? unlockedAt;

  AchievementItem({
    required this.code,
    required this.title,
    required this.description,
    required this.iconName,
    required this.xpReward,
    required this.isUnlocked,
    this.unlockedAt,
  });

  factory AchievementItem.fromJson(Map<String, dynamic> json) {
    int parseInt(dynamic val, [int fallback = 0]) {
      if (val is num) return val.toInt();
      if (val is String) return int.tryParse(val) ?? fallback;
      return fallback;
    }

    bool parseBool(dynamic val, [bool fallback = false]) {
      if (val is bool) return val;
      if (val is String) return val.toLowerCase() == 'true';
      return fallback;
    }

    DateTime? parsedDate;
    final dateVal = json['unlockedAt'] ?? json['UnlockedAt'];
    if (dateVal != null) {
      parsedDate = DateTime.tryParse(dateVal.toString());
    }

    return AchievementItem(
      code: (json['code'] ?? json['Code'] ?? '').toString(),
      title: (json['title'] ?? json['Title'] ?? '').toString(),
      description: (json['description'] ?? json['Description'] ?? '').toString(),
      iconName: (json['iconName'] ?? json['IconName'] ?? '').toString(),
      xpReward: parseInt(json['xpReward'] ?? json['XpReward'], 0),
      isUnlocked: parseBool(json['isUnlocked'] ?? json['IsUnlocked'], false),
      unlockedAt: parsedDate,
    );
  }
}
