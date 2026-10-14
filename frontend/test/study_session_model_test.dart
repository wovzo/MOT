import 'package:flutter_test/flutter_test.dart';
import 'package:mind_on_track/features/timer/data/models/study_session_model.dart';

void main() {
  group('StudySession Model Tests', () {
    test('fromJson parses completed study session correctly', () {
      final json = {
        'id': 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d',
        'title': 'Mathematics Focus',
        'startTime': '2026-10-03T10:00:00Z',
        'endTime': '2026-10-03T10:45:00Z',
        'durationMinutes': 45,
        'isCompleted': true,
        'isPaused': false,
        'accumulatedSeconds': 2700,
        'elapsedSeconds': 2700,
      };

      final session = StudySession.fromJson(json);

      expect(session.id, 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d');
      expect(session.title, 'Mathematics Focus');
      expect(session.durationMinutes, 45);
      expect(session.isCompleted, true);
      expect(session.isPaused, false);
      expect(session.accumulatedSeconds, 2700);
      expect(session.elapsedSeconds, 2700);
      expect(session.endTime, isNotNull);
    });

    test('fromJson parses active running study session correctly', () {
      final now = DateTime.now().toUtc();
      final startTimeStr = now.subtract(const Duration(minutes: 5)).toIso8601String();
      final json = {
        'id': 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e',
        'title': 'Active Deep Work',
        'startTime': startTimeStr,
        'endTime': null,
        'durationMinutes': 0,
        'isCompleted': false,
        'isPaused': false,
        'lastPausedAt': null,
        'accumulatedSeconds': 100,
        'elapsedSeconds': 300,
      };

      final session = StudySession.fromJson(json);

      expect(session.id, 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e');
      expect(session.title, 'Active Deep Work');
      expect(session.durationMinutes, 0);
      expect(session.isCompleted, false);
      expect(session.endTime, isNull);
      expect(session.isPaused, false);
      expect(session.lastPausedAt, isNull);
      expect(session.accumulatedSeconds, 100);
      expect(session.elapsedSeconds, 300);
    });

    test('fromJson parses active paused study session correctly', () {
      final json = {
        'id': 'c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f',
        'title': 'Paused Reading Session',
        'startTime': '2026-10-14T10:00:00Z',
        'endTime': null,
        'durationMinutes': 0,
        'isCompleted': false,
        'isPaused': true,
        'lastPausedAt': '2026-10-14T10:15:00Z',
        'accumulatedSeconds': 900,
        'elapsedSeconds': 900,
      };

      final session = StudySession.fromJson(json);

      expect(session.id, 'c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f');
      expect(session.isPaused, true);
      expect(session.lastPausedAt, isNotNull);
      expect(session.lastPausedAt!.toUtc().toIso8601String(), contains('2026-10-14T10:15:00'));
      expect(session.accumulatedSeconds, 900);
      expect(session.elapsedSeconds, 900);
    });

    test('fromJson applies safe fallbacks when pause/elapsed fields are omitted', () {
      final json = {
        'id': 'd4e5f6a7-b8c9-0d1e-2f3a-4b5c6d7e8f90',
        'title': 'Legacy Payload Session',
        'startTime': '2026-10-14T08:00:00Z',
        'endTime': '2026-10-14T08:25:00Z',
        'durationMinutes': 25,
        'isCompleted': true,
      };

      final session = StudySession.fromJson(json);

      expect(session.isPaused, false);
      expect(session.lastPausedAt, isNull);
      expect(session.accumulatedSeconds, 0);
      expect(session.elapsedSeconds, 1500); // 25 * 60
    });

    test('toJson serializes pause and elapsed fields faithfully', () {
      final session = StudySession(
        id: 'test-id',
        title: 'Serialization Test',
        startTime: DateTime.parse('2026-10-14T09:00:00Z'),
        endTime: null,
        durationMinutes: 10,
        isCompleted: false,
        isPaused: true,
        lastPausedAt: DateTime.parse('2026-10-14T09:10:00Z'),
        accumulatedSeconds: 600,
        elapsedSeconds: 600,
      );

      final json = session.toJson();

      expect(json['id'], 'test-id');
      expect(json['title'], 'Serialization Test');
      expect(json['isPaused'], true);
      expect(json['lastPausedAt'], contains('2026-10-14T09:10:00'));
      expect(json['accumulatedSeconds'], 600);
      expect(json['elapsedSeconds'], 600);
    });
  });
}
