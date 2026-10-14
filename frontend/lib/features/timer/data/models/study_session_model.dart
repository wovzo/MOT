class StudySession {
  final String id;
  final String title;
  final DateTime startTime;
  final DateTime? endTime;
  final int durationMinutes;
  final bool isCompleted;
  final bool isPaused;
  final DateTime? lastPausedAt;
  final int accumulatedSeconds;
  final int elapsedSeconds;

  StudySession({
    required this.id,
    required this.title,
    required this.startTime,
    this.endTime,
    required this.durationMinutes,
    required this.isCompleted,
    this.isPaused = false,
    this.lastPausedAt,
    this.accumulatedSeconds = 0,
    this.elapsedSeconds = 0,
  });

  factory StudySession.fromJson(Map<String, dynamic> json) {
    final durationMins = json['durationMinutes'] is int ? json['durationMinutes'] as int : 0;
    final accumulated = json['accumulatedSeconds'] is int ? json['accumulatedSeconds'] as int : 0;
    final elapsed = json['elapsedSeconds'] is int
        ? json['elapsedSeconds'] as int
        : (accumulated > 0 ? accumulated : durationMins * 60);

    return StudySession(
      id: json['id']?.toString() ?? '',
      title: json['title']?.toString() ?? '',
      startTime: DateTime.parse(json['startTime']).toLocal(),
      endTime: json['endTime'] != null ? DateTime.parse(json['endTime']).toLocal() : null,
      durationMinutes: durationMins,
      isCompleted: json['isCompleted'] == true,
      isPaused: json['isPaused'] == true,
      lastPausedAt: json['lastPausedAt'] != null ? DateTime.parse(json['lastPausedAt']).toLocal() : null,
      accumulatedSeconds: accumulated,
      elapsedSeconds: elapsed,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'title': title,
      'startTime': startTime.toIso8601String(),
      'endTime': endTime?.toIso8601String(),
      'durationMinutes': durationMinutes,
      'isCompleted': isCompleted,
      'isPaused': isPaused,
      'lastPausedAt': lastPausedAt?.toIso8601String(),
      'accumulatedSeconds': accumulatedSeconds,
      'elapsedSeconds': elapsedSeconds,
    };
  }
}
