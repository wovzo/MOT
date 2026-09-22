class StudySession {
  final String id;
  final String title;
  final DateTime startTime;
  final DateTime? endTime;
  final int durationMinutes;
  final bool isCompleted;

  StudySession({
    required this.id,
    required this.title,
    required this.startTime,
    this.endTime,
    required this.durationMinutes,
    required this.isCompleted,
  });

  factory StudySession.fromJson(Map<String, dynamic> json) {
    return StudySession(
      id: json['id'],
      title: json['title'],
      startTime: DateTime.parse(json['startTime']).toLocal(),
      endTime: json['endTime'] != null ? DateTime.parse(json['endTime']).toLocal() : null,
      durationMinutes: json['durationMinutes'],
      isCompleted: json['isCompleted'],
    );
  }
}
