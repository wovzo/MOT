import 'package:flutter_test/flutter_test.dart';
import 'package:mind_on_track/features/habits/data/models/habit_model.dart';

void main() {
  group('Habit Model Tests', () {
    test('fromJson parses normal habit with active streak correctly', () {
      final json = {
        'id': 'c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f',
        'title': 'Read 30 mins',
        'description': 'Read CS textbooks every day',
        'isCompletedToday': true,
        'currentStreak': 7,
      };

      final habit = Habit.fromJson(json);

      expect(habit.id, 'c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f');
      expect(habit.title, 'Read 30 mins');
      expect(habit.description, 'Read CS textbooks every day');
      expect(habit.isCompletedToday, true);
      expect(habit.currentStreak, 7);
    });

    test('fromJson parses uncompleted habit with zero streak correctly', () {
      final json = {
        'id': 'd4e5f6a7-b8c9-0d1e-2f3a-4b5c6d7e8f9a',
        'title': 'Morning Run',
        'description': 'Run 5km',
        'isCompletedToday': false,
        'currentStreak': 0,
      };

      final habit = Habit.fromJson(json);

      expect(habit.id, 'd4e5f6a7-b8c9-0d1e-2f3a-4b5c6d7e8f9a');
      expect(habit.title, 'Morning Run');
      expect(habit.description, 'Run 5km');
      expect(habit.isCompletedToday, false);
      expect(habit.currentStreak, 0);
    });

    test('fromJson safely falls back to defaults when fields are missing or null', () {
      final json = <String, dynamic>{};

      final habit = Habit.fromJson(json);

      expect(habit.id, '');
      expect(habit.title, '');
      expect(habit.description, '');
      expect(habit.isCompletedToday, false);
      expect(habit.currentStreak, 0);
    });
  });
}
