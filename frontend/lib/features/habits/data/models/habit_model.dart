class Habit {
  final String id;
  final String title;
  final String description;
  final bool isCompletedToday;
  final int currentStreak;

  Habit({
    required this.id,
    required this.title,
    required this.description,
    required this.isCompletedToday,
    required this.currentStreak,
  });

  factory Habit.fromJson(Map<String, dynamic> json) {
    return Habit(
      id: json['id'] ?? '',
      title: json['title'] ?? '',
      description: json['description'] ?? '',
      isCompletedToday: json['isCompletedToday'] ?? false,
      currentStreak: json['currentStreak'] ?? 0,
    );
  }
}
