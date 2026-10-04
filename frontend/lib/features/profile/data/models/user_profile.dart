class UserProfile {
  final String id;
  final String email;
  final String displayName;
  final DateTime createdAt;
  final int currentStreak;
  final int level;
  final int xp;

  UserProfile({
    required this.id,
    required this.email,
    required this.displayName,
    required this.createdAt,
    required this.currentStreak,
    required this.level,
    required this.xp,
  });

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    DateTime parsedDate;
    try {
      parsedDate = json['createdAt'] != null
          ? (DateTime.tryParse(json['createdAt'].toString()) ?? DateTime.now())
          : DateTime.now();
    } catch (_) {
      parsedDate = DateTime.now();
    }

    return UserProfile(
      id: (json['id'] ?? '').toString(),
      email: (json['email'] ?? '').toString(),
      displayName: (json['displayName'] ?? '').toString(),
      createdAt: parsedDate,
      currentStreak: json['currentStreak'] is num
          ? (json['currentStreak'] as num).toInt()
          : 0,
      level: json['level'] is num ? (json['level'] as num).toInt() : 1,
      xp: json['xp'] is num ? (json['xp'] as num).toInt() : 0,
    );
  }
}
