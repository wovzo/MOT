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
      };

      final session = StudySession.fromJson(json);

      expect(session.id, 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d');
      expect(session.title, 'Mathematics Focus');
      expect(session.durationMinutes, 45);
      expect(session.isCompleted, true);
      expect(session.endTime, isNotNull);
    });

    test('fromJson parses active study session with null endTime correctly', () {
      final json = {
        'id': 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e',
        'title': 'Active Deep Work',
        'startTime': '2026-10-03T12:00:00Z',
        'endTime': null,
        'durationMinutes': 0,
        'isCompleted': false,
      };

      final session = StudySession.fromJson(json);

      expect(session.id, 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e');
      expect(session.title, 'Active Deep Work');
      expect(session.durationMinutes, 0);
      expect(session.isCompleted, false);
      expect(session.endTime, isNull);
    });
  });
}
