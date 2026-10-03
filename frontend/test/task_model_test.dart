import 'package:flutter_test/flutter_test.dart';
import 'package:mind_on_track/features/tasks/data/models/daily_task.dart';

void main() {
  group('DailyTask Model Tests', () {
    test('fromJson parses normal completed task correctly', () {
      final json = {
        'id': 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d',
        'title': 'Complete Homework',
        'description': 'Finish math problem set 4',
        'isCompleted': true,
        'createdAt': '2026-10-03T10:00:00.000Z',
      };

      final task = DailyTask.fromJson(json);

      expect(task.id, 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d');
      expect(task.title, 'Complete Homework');
      expect(task.description, 'Finish math problem set 4');
      expect(task.isCompleted, true);
      expect(task.createdAt, DateTime.parse('2026-10-03T10:00:00.000Z'));
    });

    test('fromJson parses uncompleted task with false completion state correctly', () {
      final json = {
        'id': 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e',
        'title': 'Review PR',
        'description': null,
        'isCompleted': false,
        'createdAt': '2026-10-03T11:00:00.000Z',
      };

      final task = DailyTask.fromJson(json);

      expect(task.id, 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e');
      expect(task.title, 'Review PR');
      expect(task.description, isNull);
      expect(task.isCompleted, false);
      expect(task.createdAt, DateTime.parse('2026-10-03T11:00:00.000Z'));
    });

    test('fromJson safely falls back to defaults when fields are missing or null', () {
      final json = <String, dynamic>{};

      final task = DailyTask.fromJson(json);

      expect(task.id, '');
      expect(task.title, '');
      expect(task.description, isNull);
      expect(task.isCompleted, false);
      expect(task.createdAt, isA<DateTime>());
    });
  });
}
